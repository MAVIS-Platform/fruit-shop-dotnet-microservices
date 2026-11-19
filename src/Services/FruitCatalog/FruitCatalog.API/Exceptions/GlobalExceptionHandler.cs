using FruitCatalog.API.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FruitCatalog.API.Exceptions;

/// <summary>
/// Maps domain and concurrency exceptions to RFC 7807 ProblemDetails responses.
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
            ProductNotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Product Not Found",
                notFound.Message),

            InsufficientStockException insufficient => (
                StatusCodes.Status409Conflict,
                "Insufficient Stock",
                insufficient.Message),

            DuplicateSkuException duplicate => (
                StatusCodes.Status409Conflict,
                "Duplicate SKU",
                duplicate.Message),

            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "Concurrency Conflict",
                "The product inventory was modified concurrently by another request. Please retry."),

            DomainException or ArgumentException or BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                exception.Message),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred while processing the catalog request.")
        };

        if (statusCode >= 500)
        {
            logger.LogError(exception, "Unhandled catalog exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Handled catalog exception ({StatusCode}): {Message}", statusCode, exception.Message);
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
