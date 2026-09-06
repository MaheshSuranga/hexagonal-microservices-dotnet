namespace OrderApi.Contracts.Events;

/// <summary>
/// Integration Event published to the message broker when an order has been placed.
/// Designed as an immutable record with value-based equality and serialization support.
/// </summary>
public record OrderPlacedEvent(
    Guid OrderId,
    string UserId,
    decimal TotalAmount,
    DateTime OccurredAtUtc,
    // Recommended Envelope Metadata:
    Guid EventId,
    int SchemaVersion = 1
)
{
    /// <summary>
    /// Convenience factory for creating the event with fresh metadata.
    /// </summary>
    public static OrderPlacedEvent Create(Guid orderId, string userId, decimal totalAmount) =>
        new(
            OrderId: orderId,
            UserId: userId,
            TotalAmount: totalAmount,
            OccurredAtUtc: DateTime.UtcNow,
            EventId: Guid.NewGuid(),
            SchemaVersion: 1
        );
}
