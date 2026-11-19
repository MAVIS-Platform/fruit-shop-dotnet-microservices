using System.Text.Json;
using FruitBasket.API.Models;
using Microsoft.Extensions.Caching.Distributed;

namespace FruitBasket.API.Repositories;

public sealed class RedisBasketRepository(IDistributedCache cache) : IBasketRepository
{
    private static readonly TimeSpan BasketTtl = TimeSpan.FromDays(7);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static string GetKey(string customerId) => $"basket:{customerId}";

    public async Task<CustomerBasket?> GetBasketAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var payload = await cache.GetStringAsync(GetKey(customerId), cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        return JsonSerializer.Deserialize<CustomerBasket>(payload, JsonOptions);
    }

    public async Task<CustomerBasket> UpdateBasketAsync(CustomerBasket basket, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(basket, JsonOptions);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = BasketTtl
        };

        await cache.SetStringAsync(GetKey(basket.CustomerId), payload, options, cancellationToken);
        return basket;
    }

    public async Task DeleteBasketAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(GetKey(customerId), cancellationToken);
    }
}
