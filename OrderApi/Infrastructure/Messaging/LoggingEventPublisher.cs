namespace OrderApi.Infrastructure.Messaging;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrderApi.Domain.Ports;

/// <summary>
/// Development fallback adapter for IEventPublisher when no Service Bus connection is configured.
/// Logs event publication to console and allows offline execution.
/// </summary>
public class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger<LoggingEventPublisher> _logger;

    public LoggingEventPublisher(ILogger<LoggingEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<T>(T @event, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(@event, new JsonSerializerOptions { WriteIndented = true });
        _logger.LogInformation(
            "📢 [LocalEventPublisher] (Offline Fallback) Event '{EventType}' published:\n{Payload}",
            typeof(T).Name, json);
        return Task.CompletedTask;
    }
}
