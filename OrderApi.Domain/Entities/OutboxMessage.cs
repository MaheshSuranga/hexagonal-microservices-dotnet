namespace OrderApi.Domain.Entities;

/// <summary>
/// Represents an integration event queued in the database outbox for guaranteed,
/// at-least-once asynchronous delivery to the message broker.
/// Committed in the exact same database transaction as the business entity.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public DateTime OccurredOnUtc { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }

    // Parameterless constructor for ORM reconstitution
    private OutboxMessage() { }

    public OutboxMessage(Guid id, DateTime occurredOnUtc, string type, string payload)
    {
        Id = id;
        OccurredOnUtc = occurredOnUtc;
        Type = type;
        Payload = payload;
        ProcessedOnUtc = null;
        Error = null;
    }

    public static OutboxMessage Create(string type, string payload)
    {
        return new OutboxMessage(Guid.NewGuid(), DateTime.UtcNow, type, payload);
    }

    public void MarkAsProcessed(DateTime processedOnUtc)
    {
        ProcessedOnUtc = processedOnUtc;
        Error = null;
    }

    public void MarkAsFailed(string error)
    {
        Error = error;
    }
}
