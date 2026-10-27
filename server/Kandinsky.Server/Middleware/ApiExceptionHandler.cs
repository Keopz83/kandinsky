using Kandinsky.Server.Diagrams;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kandinsky.Server.Middleware;

/// <summary>Maps domain exceptions to problem details responses.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found", exception.Message),
            DiagramValidationException => (StatusCodes.Status400BadRequest, "Bad Request", exception.Message),
            BadHttpRequestException badRequest => (badRequest.StatusCode, "Bad Request", "The request is malformed."),
            _ => (0, null, null),
        };
        if (status == 0)
        {
            return false;
        }

        logger.LogInformation("Request failed with {Status}: {Detail}", status, detail);
        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
