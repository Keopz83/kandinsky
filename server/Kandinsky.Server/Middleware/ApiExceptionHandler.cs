using Kandinsky.Server.Diagrams;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kandinsky.Server.Middleware;

/// <summary>Maps domain exceptions to problem details responses; other exceptions are left to the default handler.</summary>
internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetails, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    private const string BadRequestTitle = "Bad Request";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (ToProblem(exception) is not { } problem)
        {
            return false;
        }

        logger.LogInformation("Request failed with {Status}: {Detail}", problem.Status, problem.Detail);
        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
    }

    // Domain exception messages are safe to return to clients; parser messages of malformed requests are not.
    private static ProblemDetails? ToProblem(Exception exception) => exception switch
    {
        NotFoundException => Problem(StatusCodes.Status404NotFound, "Not Found", exception.Message),
        DiagramValidationException => Problem(StatusCodes.Status400BadRequest, BadRequestTitle, exception.Message),
        BadHttpRequestException badRequest => Problem(badRequest.StatusCode, BadRequestTitle, "The request is malformed."),
        _ => null,
    };

    private static ProblemDetails Problem(int status, string title, string detail) => new() { Status = status, Title = title, Detail = detail };
}
