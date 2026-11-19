using System.Text.Json;

namespace FruitOrdering.API.Domain;

public enum OutboxEventState
{
    NotPublished = 0,
    Published = 1
}

public sealed class IntegrationEventLogEntry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private IntegrationEventLogEntry()
    {
    }

    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string PayloadJson { get; private set; } = string.Empty;
    public OutboxEventState State { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? PublishedAtUtc { get; private set; }

    public static IntegrationEventLogEntry Create<TEvent>(Guid eventId, TEvent integrationEvent)
        where TEvent : class
    {
        return new IntegrationEventLogEntry
        {
            EventId = eventId,
            EventType = typeof(TEvent).Name,
            PayloadJson = JsonSerializer.Serialize(integrationEvent, JsonOptions),
            State = OutboxEventState.NotPublished,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkAsPublished()
    {
        State = OutboxEventState.Published;
        PublishedAtUtc = DateTime.UtcNow;
    }
}
