namespace OrderApi.Tests.Fakes;

using OrderApi.Domain.Entities;
using OrderApi.Domain.Ports;

/// <summary>
/// A fast, pure in-memory test double (Fake) satisfying the driven port IOrderRepository.
/// Zero EF Core, zero SQL, zero mocking library ceremony.
/// </summary>
public class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<Guid, Order> _store = new();
    private readonly List<OutboxMessage> _outbox = new();

    public Task AddAsync(Order order, CancellationToken ct = default)
    {
        _store[order.Id] = order;
        return Task.CompletedTask;
    }

    public Task AddWithOutboxAsync(Order order, OutboxMessage outboxMessage, CancellationToken ct = default)
    {
        _store[order.Id] = order;
        _outbox.Add(outboxMessage);
        return Task.CompletedTask;
    }

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var order);
        return Task.FromResult(order);
    }

    // Helpers for test verification:
    public int Count => _store.Count;
    public IReadOnlyList<OutboxMessage> OutboxMessages => _outbox.AsReadOnly();
}
