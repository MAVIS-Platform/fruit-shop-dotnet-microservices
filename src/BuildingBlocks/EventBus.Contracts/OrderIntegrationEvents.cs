namespace EventBus.Contracts;

/// <summary>
/// Published via Outbox when an order is confirmed and stock has been reserved.
/// </summary>
public sealed record OrderCreatedIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    string CorrelationId,
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount);

/// <summary>
/// Published via Outbox when an order saga fails or is cancelled after compensation.
/// </summary>
public sealed record OrderCancelledIntegrationEvent(
    Guid EventId,
    DateTime OccurredAtUtc,
    string CorrelationId,
    Guid OrderId,
    string CustomerId,
    string Reason);
