using System.Text.Json;
using EventBus.Contracts;
using FruitOrdering.API.Data;
using FruitOrdering.API.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace FruitOrdering.API.BackgroundServices;

/// <summary>
/// Relays unpublished Transactional Outbox entries to RabbitMQ via MassTransit.
/// Uses IServiceScopeFactory to avoid Captive Dependency of Scoped DbContext/IPublishEndpoint.
/// </summary>
public sealed class OutboxRelayBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxRelayBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("OutboxRelayBackgroundService started.");
        using var timer = new PeriodicTimer(PollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessPendingOutboxBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while relaying outbox integration events.");
            }
        }
    }

    private async Task ProcessPendingOutboxBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var pendingEntries = await dbContext.IntegrationEventLogEntries
            .Where(e => e.State == OutboxEventState.NotPublished)
            .OrderBy(e => e.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (pendingEntries.Count == 0)
            return;

        foreach (var entry in pendingEntries)
        {
            var published = await PublishEntryAsync(publishEndpoint, entry, cancellationToken);
            if (published)
            {
                entry.MarkAsPublished();
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> PublishEntryAsync(
        IPublishEndpoint publishEndpoint,
        IntegrationEventLogEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry.EventType == nameof(OrderCreatedIntegrationEvent))
        {
            var evt = JsonSerializer.Deserialize<OrderCreatedIntegrationEvent>(entry.PayloadJson, JsonOptions);
            if (evt is null)
            {
                logger.LogWarning("Failed to deserialize Outbox event {EventId} of type {EventType}.", entry.EventId, entry.EventType);
                return false;
            }

            await publishEndpoint.Publish(evt, cancellationToken);
            logger.LogInformation("Relayed OrderCreatedIntegrationEvent {EventId} for Order {OrderId}.", evt.EventId, evt.OrderId);
            return true;
        }

        if (entry.EventType == nameof(OrderCancelledIntegrationEvent))
        {
            var evt = JsonSerializer.Deserialize<OrderCancelledIntegrationEvent>(entry.PayloadJson, JsonOptions);
            if (evt is null)
            {
                logger.LogWarning("Failed to deserialize Outbox event {EventId} of type {EventType}.", entry.EventId, entry.EventType);
                return false;
            }

            await publishEndpoint.Publish(evt, cancellationToken);
            logger.LogInformation("Relayed OrderCancelledIntegrationEvent {EventId} for Order {OrderId}.", evt.EventId, evt.OrderId);
            return true;
        }

        logger.LogWarning("Unknown Outbox event type '{EventType}' for EventId {EventId}.", entry.EventType, entry.EventId);
        return false;
    }
}
