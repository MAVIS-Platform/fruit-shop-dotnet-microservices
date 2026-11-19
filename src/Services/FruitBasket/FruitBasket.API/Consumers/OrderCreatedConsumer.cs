using EventBus.Contracts;
using FruitBasket.API.Repositories;
using FruitBasket.API.Services;
using MassTransit;

namespace FruitBasket.API.Consumers;

/// <summary>
/// Idempotent MassTransit consumer that clears a customer's Redis basket once an order is confirmed.
/// </summary>
public sealed class OrderCreatedConsumer(
    IBasketService basketService,
    IProcessedEventStore processedEventStore,
    ILogger<OrderCreatedConsumer> logger) : IConsumer<OrderCreatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var message = context.Message;

        if (await processedEventStore.HasProcessedAsync(message.EventId, context.CancellationToken))
        {
            logger.LogInformation(
                "Duplicate OrderCreatedIntegrationEvent {EventId} for Order {OrderId} ignored.",
                message.EventId, message.OrderId);
            return;
        }

        await basketService.DeleteBasketAsync(message.CustomerId, context.CancellationToken);
        await processedEventStore.MarkProcessedAsync(message.EventId, context.CancellationToken);

        logger.LogInformation(
            "Processed OrderCreatedIntegrationEvent {EventId}: cleared basket for {CustomerId} (Order {OrderId}, CorrelationId {CorrelationId}).",
            message.EventId, message.CustomerId, message.OrderId, message.CorrelationId);
    }
}
