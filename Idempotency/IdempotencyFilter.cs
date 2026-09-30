using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyFirstApi.Data;
using MyFirstApi.Exceptions;
using MyFirstApi.Interfaces;
using MyFirstApi.Models;

namespace MyFirstApi.Idempotency;

// Makes a POST safe to retry. When the request carries "Idempotency-Key: <value>":
// - first time: the action runs inside one transaction together with saving the key
//   and its response, so either both are committed or neither is;
// - retry with the same key and the same request: the stored response is returned
//   (header Idempotent-Replayed: true) and nothing runs again;
// - same key with a different request: 422 IDEMPOTENCY_KEY_REUSED;
// - only successful (2xx) responses are stored, so a failed request can be retried
//   with the same key after fixing the problem.
// Two concurrent requests with one key: the second one's insert waits on the key row
// until the first commits, then replays its response.
// Without the header the action runs normally. Keys are per user and expire after
// Idempotency:RetentionHours (default 24).
public class IdempotencyFilter : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";
    public const string ReplayedHeaderName = "Idempotent-Replayed";
    public const int MaxKeyLength = 255;

    private static readonly JsonSerializerOptions HashJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new FormFileHashConverter() }
    };

    private readonly AppDbContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IUrlHelperFactory _urlHelperFactory;
    private readonly JsonSerializerOptions _responseJsonOptions;
    private readonly TimeSpan _retention;

    public IdempotencyFilter(
        AppDbContext context,
        ICurrentUser currentUser,
        IUrlHelperFactory urlHelperFactory,
        IOptions<JsonOptions> jsonOptions,
        IConfiguration configuration)
    {
        _context = context;
        _currentUser = currentUser;
        _urlHelperFactory = urlHelperFactory;
        _responseJsonOptions = jsonOptions.Value.JsonSerializerOptions;
        _retention = TimeSpan.FromHours(configuration.GetValue("Idempotency:RetentionHours", 24));
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!request.Headers.TryGetValue(HeaderName, out var values))
        {
            await next();
            return;
        }

        var key = values.Count == 1 ? values[0] ?? string.Empty : string.Empty;
        if (key.Length is 0 or > MaxKeyLength || key.Any(c => c is < '!' or > '~'))
        {
            throw Errors.IdempotencyKeyInvalid(MaxKeyLength);
        }

        var username = _currentUser.Username ?? string.Empty;
        var path = request.Path.ToString() + request.QueryString;
        var hash = ComputeHash(request.Method, path, context.ActionArguments);
        var now = DateTime.UtcNow;

        var existing = await FindAsync(username, key);
        if (existing != null)
        {
            if (existing.ExpiresAt > now)
            {
                context.Result = Replay(context, existing, hash);
                return;
            }
            await _context.IdempotencyKeys
                .Where(k => k.Username == username && k.Key == key)
                .ExecuteDeleteAsync();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        var requestPath = path.Length > 500 ? path[..500] : path;
        // Waits if a concurrent request holds this key uncommitted; 0 rows once it commits.
        var inserted = await _context.Database.ExecuteSqlAsync($"""
            INSERT INTO "IdempotencyKeys"
                ("Username", "Key", "RequestMethod", "RequestPath", "RequestHash", "CreatedAt", "ExpiresAt")
            VALUES ({username}, {key}, {request.Method}, {requestPath}, {hash}, {now}, {now + _retention})
            ON CONFLICT DO NOTHING
            """);
        if (inserted == 0)
        {
            await transaction.RollbackAsync();
            context.Result = Replay(context, (await FindAsync(username, key))!, hash);
            return;
        }

        var executed = await next();
        // Exceptions and non-2xx results: the transaction is rolled back on dispose,
        // so the key is not kept and the client may retry with it.
        if (executed.Exception != null && !executed.ExceptionHandled) return;

        var (status, body, location) = CaptureResponse(executed);
        if (status is < 200 or >= 300) return;

        await _context.IdempotencyKeys
            .Where(k => k.Username == username && k.Key == key)
            .ExecuteUpdateAsync(k => k
                .SetProperty(x => x.ResponseStatusCode, status)
                .SetProperty(x => x.ResponseBody, body)
                .SetProperty(x => x.ResponseLocation, location));
        await transaction.CommitAsync();
    }

    private Task<IdempotencyKey?> FindAsync(string username, string key) =>
        _context.IdempotencyKeys.AsNoTracking()
            .FirstOrDefaultAsync(k => k.Username == username && k.Key == key);

    private static IActionResult Replay(ActionExecutingContext context, IdempotencyKey stored, string hash)
    {
        if (stored.RequestHash != hash)
        {
            throw Errors.IdempotencyKeyReused(stored.Key);
        }

        var headers = context.HttpContext.Response.Headers;
        headers[ReplayedHeaderName] = "true";
        if (stored.ResponseLocation != null)
        {
            headers.Location = stored.ResponseLocation;
        }

        var status = stored.ResponseStatusCode ?? StatusCodes.Status200OK;
        return stored.ResponseBody == null
            ? new StatusCodeResult(status)
            : new ContentResult { StatusCode = status, Content = stored.ResponseBody, ContentType = "application/json; charset=utf-8" };
    }

    private (int Status, string? Body, string? Location) CaptureResponse(ActionExecutedContext executed)
    {
        switch (executed.Result)
        {
            case ObjectResult result:
                var status = result.StatusCode ?? StatusCodes.Status200OK;
                var body = result.Value == null
                    ? null
                    : JsonSerializer.Serialize(result.Value, result.Value.GetType(), _responseJsonOptions);
                return (status, body, GetLocation(executed, result));
            case StatusCodeResult result:
                return (result.StatusCode, null, null);
            default:
                throw new InvalidOperationException(
                    $"[Idempotent] action returned {executed.Result?.GetType().Name}, which can't be stored.");
        }
    }

    private string? GetLocation(ActionExecutedContext executed, ObjectResult result)
    {
        var request = executed.HttpContext.Request;
        return result switch
        {
            CreatedAtActionResult created => _urlHelperFactory.GetUrlHelper(executed).Action(
                created.ActionName, created.ControllerName, created.RouteValues, request.Scheme, request.Host.ToUriComponent()),
            CreatedResult created => created.Location,
            _ => null
        };
    }

    // Method + path + the bound action arguments (after model binding, so formatting
    // differences in the raw body don't matter).
    private static string ComputeHash(string method, string path, IDictionary<string, object?> arguments)
    {
        var payload = JsonSerializer.Serialize(new
        {
            method,
            path,
            arguments = arguments.Where(a => a.Value is not CancellationToken)
                .ToDictionary(a => a.Key, a => a.Value)
        }, HashJsonOptions);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    // Uploaded files take part in the hash by name, size and content hash.
    private sealed class FormFileHashConverter : JsonConverter<IFormFile>
    {
        public override IFormFile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, IFormFile value, JsonSerializerOptions options)
        {
            using var stream = value.OpenReadStream();
            writer.WriteStartObject();
            writer.WriteString("name", value.Name);
            writer.WriteString("fileName", value.FileName);
            writer.WriteNumber("length", value.Length);
            writer.WriteString("sha256", Convert.ToHexStringLower(SHA256.HashData(stream)));
            writer.WriteEndObject();
        }
    }
}
