using FruitOrdering.API.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FruitOrdering.API.Exceptions;

/// <summary>
/// Maps ordering domain, IDOR, stock conflict, and Polly resilience exceptions to RFC 7807 ProblemDetails.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            OrderNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Order Not Found",
                notFound.Message),

            ForbiddenAccessException forbidden => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                forbidden.Message),

            OrderConflictException conflict => (
                StatusCodes.Status409Conflict,
                "Order Conflict",
                conflict.Message),

            ServiceUnavailableException unavailable => (
                StatusCodes.Status503ServiceUnavailable,
                "Downstream Service Unavailable",
                unavailable.Message),

            DomainException or ArgumentException or BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                exception.Message),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication is required."),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing the order.")
        };

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Ordering exception ({StatusCode}) on {Method} {Path}",
                statusCode, httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Handled ordering exception ({StatusCode}): {Message}", statusCode, exception.Message);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
