namespace OrderApi.Infrastructure.Messaging;

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using OrderApi.Domain.Ports;

/// <summary>
/// Driven Adapter implementing IEventPublisher using Azure.Messaging.ServiceBus.
/// Leverages a singleton ServiceBusClient and ServiceBusSender to prevent socket exhaustion.
/// </summary>
public class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusEventPublisher> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ServiceBusEventPublisher(
        ServiceBusClient serviceBusClient,
        ILogger<ServiceBusEventPublisher> logger,
        string topicName = "order-placed-topic")
    {
        _logger = logger;
        // ServiceBusSender is thread-safe and safe to cache for the lifetime of the application
        _sender = serviceBusClient.CreateSender(topicName);
    }

    public async Task PublishAsync<T>(T @event, CancellationToken ct = default)
    {
        var eventType = typeof(T).Name;
        var payload = JsonSerializer.Serialize(@event, JsonOptions);

        var message = new ServiceBusMessage(BinaryData.FromString(payload))
        {
            ContentType = "application/json",
            Subject = eventType,
            MessageId = Guid.NewGuid().ToString()
        };

        // Enrich with tracing metadata
        message.ApplicationProperties["EventType"] = eventType;
        message.ApplicationProperties["PublishedAtUtc"] = DateTime.UtcNow.ToString("O");

        _logger.LogInformation(
            "[ServiceBusEventPublisher] Publishing {EventType} (MessageId: {MessageId}) to topic '{Topic}'",
            eventType, message.MessageId, _sender.EntityPath);

        await _sender.SendMessageAsync(message, ct);

        _logger.LogInformation(
            "[ServiceBusEventPublisher] Successfully published {EventType} (MessageId: {MessageId})",
            eventType, message.MessageId);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
    }
}
