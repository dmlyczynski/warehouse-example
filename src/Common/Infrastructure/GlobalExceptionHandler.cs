using System.Diagnostics;
using System.Security.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Common.Infrastructure;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        await HandleExceptionAsync(httpContext, exception, cancellationToken);

        return true;
    }

    private Task HandleExceptionAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var activity = GetActivity(context);
        var problemDetails = GetProblemDetails(exception, context);

        LogInternalServerError(exception, problemDetails, activity?.Id, context.TraceIdentifier);

        context.Response.StatusCode = problemDetails.Status!.Value;

        return context.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    }

    private static Activity? GetActivity(HttpContext context)
    {
        return context.Features.Get<IHttpActivityFeature>()?.Activity;
    }

    private void LogInternalServerError(Exception exception, ProblemDetails problemDetails, string? activityId, string traceIdentifier)
    {
        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            problemDetails.Extensions.TryAdd("traceId", traceIdentifier);
            problemDetails.Extensions.TryAdd("requestId", activityId);

            _logger.LogError(exception, "Exception occurred: {Message}, TraceId: {TraceId}", exception.Message, traceIdentifier);
        }
    }

    private static ProblemDetails GetProblemDetails(Exception exception, HttpContext context)
    {
        var problemDetails = GetStatusAndTitle(exception);

        problemDetails.Detail = exception.Message;
        problemDetails.Instance = $"{context.Request.Method} {context.Request.Path}";

        return problemDetails;
    }

    private static ProblemDetails GetStatusAndTitle(Exception exception)
    {
        return exception switch
        {
            BadHttpRequestException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad request"
            },
            AuthenticationException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthenticated access"
            },
            UnauthorizedAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Unauthorized access"
            },
            InvalidDataException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found"
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error"
            },
        };
    }
}