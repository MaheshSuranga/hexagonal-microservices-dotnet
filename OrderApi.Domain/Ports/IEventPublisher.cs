namespace OrderApi.Domain.Ports;

/// <summary>
/// Driven (Outbound) Port for asynchronous event publishing.
/// Keeps the Domain Core decoupled from specific message brokers like Azure Service Bus or RabbitMQ.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(T @event, CancellationToken ct = default);
}
