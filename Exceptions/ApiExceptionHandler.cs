using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MyFirstApi.Exceptions;

// Turns ApiException from services into a ProblemDetails (RFC 9457) response:
//   { "status": 409, "detail": "<English message>", "code": "TAG_CODE_EXISTS",
//     "args": { "tagCodes": ["QR-1"] }, "scope": { "kind": "item", "number": 2 }, "traceId": "..." }
// so controllers need no try/catch. Anything else falls through to the default 500.
public class ApiExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;

    public ApiExceptionHandler(IProblemDetailsService problemDetailsService)
    {
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ApiException apiException) return false;

        var problem = new ProblemDetails
        {
            Status = apiException.Definition.Status,
            Detail = apiException.Message
        };
        problem.Extensions["code"] = apiException.Definition.Code;
        problem.Extensions["args"] = apiException.Args;
        if (apiException.Scope != null)
        {
            problem.Extensions["scope"] = new { kind = apiException.Scope.Kind, number = apiException.Scope.Number };
        }

        httpContext.Response.StatusCode = apiException.Definition.Status;
        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
