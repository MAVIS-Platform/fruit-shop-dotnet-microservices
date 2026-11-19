using Microsoft.Extensions.Caching.Distributed;

namespace FruitBasket.API.Repositories;

public sealed class RedisProcessedEventStore(IDistributedCache cache) : IProcessedEventStore
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromHours(24);

    private static string GetKey(Guid eventId) => $"processed-event:{eventId:N}";

    public async Task<bool> HasProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var value = await cache.GetStringAsync(GetKey(eventId), cancellationToken);
        return value is not null;
    }

    public async Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = IdempotencyTtl
        };

        await cache.SetStringAsync(GetKey(eventId), DateTime.UtcNow.ToString("O"), options, cancellationToken);
    }
}
