using ClaimsManagement.Application.Exceptions;
using ClaimsManagement.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClaimsManagement.Api.ErrorHandling;

/// <summary>
/// Centralized exception -> ProblemDetails (RFC7807) mapping, per CLAUDE.md "Coding standards".
/// No stack traces leave the process outside Development.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = Map(exception);

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("{Title} on {Method} {Path}: {Message}", title, httpContext.Request.Method, httpContext.Request.Path, exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = statusCode == StatusCodes.Status500InternalServerError && !environment.IsDevelopment()
                ? "An unexpected error occurred."
                : exception.Message,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static (int StatusCode, string Title) Map(Exception exception) => exception switch
    {
        ClaimValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
        ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request"),
        InvalidClaimTransitionException => (StatusCodes.Status409Conflict, "Invalid claim status transition"),
        ConcurrentUpdateException => (StatusCodes.Status409Conflict, "Concurrent update conflict"),
        ClaimOwnershipException => (StatusCodes.Status403Forbidden, "Not authorized for this claim"),
        EntityNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
    };
}
