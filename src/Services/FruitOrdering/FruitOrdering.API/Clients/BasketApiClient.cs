using System.Net.Http.Headers;
using System.Net.Http.Json;
using FruitOrdering.API.Domain;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace FruitOrdering.API.Clients;

public sealed record BasketItemDto(
    int ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    string Unit);

public sealed record BasketDto(
    string CustomerId,
    List<BasketItemDto> Items);

public interface IBasketApiClient
{
    Task<BasketDto> GetBasketAsync(
        string customerId,
        string? bearerToken,
        CancellationToken cancellationToken = default);
}

public sealed class BasketApiClient(
    HttpClient httpClient,
    ILogger<BasketApiClient> logger) : IBasketApiClient
{
    private const string DownstreamName = "FruitBasket.API";

    public async Task<BasketDto> GetBasketAsync(
        string customerId,
        string? bearerToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v1/basket/{Uri.EscapeDataString(customerId)}");

            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            }

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if ((int)response.StatusCode >= 500)
            {
                throw new ServiceUnavailableException(
                    DownstreamName,
                    $"{DownstreamName} returned {(int)response.StatusCode} while fetching basket.");
            }

            response.EnsureSuccessStatusCode();
            var basket = await response.Content.ReadFromJsonAsync<BasketDto>(cancellationToken: cancellationToken);
            return basket ?? new BasketDto(customerId, []);
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
