using Microsoft.AspNetCore.Diagnostics;

namespace MyFirstApi.Exceptions;

// Maps service-layer exceptions to HTTP responses, so controllers don't need
// try/catch blocks. Anything else falls through to the default 500 ProblemDetails.
public class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = exception switch
        {
            ConflictException => StatusCodes.Status409Conflict,
            BusinessRuleException => StatusCodes.Status400BadRequest,
            _ => 0
        };
        if (statusCode == 0) return false;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new { message = exception.Message }, cancellationToken);
        return true;
    }
}
