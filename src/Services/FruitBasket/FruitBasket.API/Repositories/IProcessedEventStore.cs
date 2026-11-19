namespace FruitBasket.API.Repositories;

public interface IProcessedEventStore
{
    Task<bool> HasProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}
