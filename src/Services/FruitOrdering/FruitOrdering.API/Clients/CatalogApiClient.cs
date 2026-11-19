using System.Net;
using System.Net.Http.Json;
using FruitOrdering.API.Domain;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace FruitOrdering.API.Clients;

public sealed record CatalogFruitItemDto(
    int Id,
    string Sku,
    string Name,
    string Unit,
    decimal UnitPrice,
    int AvailableStock);

public interface ICatalogApiClient
{
    Task<CatalogFruitItemDto?> GetItemAsync(int productId, CancellationToken cancellationToken = default);
    Task ReserveStockAsync(int productId, int quantity, CancellationToken cancellationToken = default);
    Task ReleaseStockAsync(int productId, int quantity, CancellationToken cancellationToken = default);
}

public sealed class CatalogApiClient(
    HttpClient httpClient,
    ILogger<CatalogApiClient> logger) : ICatalogApiClient
{
    private const string DownstreamName = "FruitCatalog.API";

    public async Task<CatalogFruitItemDto?> GetItemAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await ExecuteResilientAsync(async () =>
        {
            using var response = await httpClient.GetAsync($"/api/v1/catalog/items/{productId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if ((int)response.StatusCode >= 500)
            {
                throw new ServiceUnavailableException(
                    DownstreamName,
                    $"{DownstreamName} returned {(int)response.StatusCode} while querying product {productId}.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<CatalogFruitItemDto>(cancellationToken: cancellationToken);
        });
    }

    public async Task ReserveStockAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        await ExecuteResilientAsync(async () =>
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"/api/v1/catalog/items/{productId}/reserve",
                new { Quantity = quantity },
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                throw new OrderConflictException($"Insufficient stock for product {productId} (requested {quantity}).");
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new DomainException($"Product {productId} was not found in catalog.");
            }

            if ((int)response.StatusCode >= 500)
            {
                throw new ServiceUnavailableException(
                    DownstreamName,
                    $"{DownstreamName} returned {(int)response.StatusCode} while reserving product {productId}.");
            }

            response.EnsureSuccessStatusCode();
            return true;
        });
    }

    public async Task ReleaseStockAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        await ExecuteResilientAsync(async () =>
        {
            using var response = await httpClient.PostAsJsonAsync(
                $"/api/v1/catalog/items/{productId}/release",
                new { Quantity = quantity },
                cancellationToken);

            if ((int)response.StatusCode >= 500)
            {
                throw new ServiceUnavailableException(
                    DownstreamName,
                    $"{DownstreamName} returned {(int)response.StatusCode} while releasing product {productId}.");
            }

            response.EnsureSuccessStatusCode();
            return true;
        });
    }

    private async Task<T> ExecuteResilientAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (BrokenCircuitException ex)
        {
            logger.LogWarning(ex, "Circuit breaker is OPEN for {DownstreamService}.", DownstreamName);
            throw new ServiceUnavailableException(
                DownstreamName,
                $"{DownstreamName} circuit breaker is open. Please retry shortly.",
                ex);
        }
        catch (TimeoutRejectedException ex)
        {
            logger.LogWarning(ex, "Resilience timeout calling {DownstreamService}.", DownstreamName);
            throw new ServiceUnavailableException(
                DownstreamName,
                $"{DownstreamName} timed out.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "HTTP failure calling {DownstreamService}.", DownstreamName);
            throw new ServiceUnavailableException(
                DownstreamName,
                $"{DownstreamName} is currently unreachable.",
                ex);
        }
    }
}
