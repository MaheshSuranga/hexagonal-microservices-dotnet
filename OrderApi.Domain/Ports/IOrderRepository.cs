namespace OrderApi.Domain.Ports;

using OrderApi.Domain.Entities;

/// <summary>
/// Driven (Outbound) Port defined by the Domain Core.
/// Expresses persistence requirements strictly in the domain's vocabulary.
/// Contains zero references to databases, SQL, or EF Core.
/// </summary>
public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken ct = default);
    Task AddWithOutboxAsync(Order order, OutboxMessage outboxMessage, CancellationToken ct = default);
    Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
