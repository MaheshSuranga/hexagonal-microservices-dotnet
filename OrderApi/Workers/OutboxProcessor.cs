namespace OrderApi.Workers;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderApi.Contracts.Events;
using OrderApi.Domain.Ports;
using OrderApi.Infrastructure;

/// <summary>
/// Resilient background worker implementing the Transactional Outbox processor.
/// Periodically polls the OutboxMessages table in batches, publishes integration events
/// to the message broker, and marks them as processed with timestamps.
/// </summary>
public class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<OutboxProcessor> _logger;

    private const int BatchSize = 20;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ErrorBackoffDelay = TimeSpan.FromSeconds(5);

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        IEventPublisher eventPublisher,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[OutboxProcessor] Started. Polling for unpublished events...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processedCount = await ProcessBatchAsync(stoppingToken);

                if (processedCount == 0)
                {
                    // Back-off delay when outbox queue is idle to preserve CPU & DB IOPS
                    await Task.Delay(IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[OutboxProcessor] Unhandled exception during batch cycle. Backing off for {Delay}s...", ErrorBackoffDelay.TotalSeconds);
                await Task.Delay(ErrorBackoffDelay, stoppingToken);
            }
        }

        _logger.LogInformation("[OutboxProcessor] Stopped gracefully.");
    }

    private async Task<int> ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        // 1. Fetch batch of unprocessed messages ordered chronologically
        // Index on (ProcessedOnUtc, OccurredOnUtc) guarantees high-throughput O(log N) retrieval
        var messages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        if (messages.Count == 0)
        {
            return 0;
        }

        _logger.LogInformation("[OutboxProcessor] Found {Count} pending outbox message(s) to dispatch.", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                // 2. Dynamically deserialize and publish based on message Type
                if (message.Type == nameof(OrderPlacedEvent))
                {
                    var @event = JsonSerializer.Deserialize<OrderPlacedEvent>(message.Payload);
                    if (@event != null)
                    {
                        await _eventPublisher.PublishAsync(@event, stoppingToken);
                    }
                }
                else
                {
                    _logger.LogWarning("[OutboxProcessor] Unrecognized event type '{Type}' for MessageId {Id}", message.Type, message.Id);
                    message.MarkAsFailed($"Unrecognized event type: {message.Type}");
                }

                // 3. Update processing timestamp (At-Least-Once Delivery checkpoint)
                message.MarkAsProcessed(DateTime.UtcNow);
                _logger.LogInformation("[OutboxProcessor] Successfully dispatched and marked OutboxMessage {MessageId} as processed.", message.Id);
            }
            catch (Exception ex)
            {
                // Fault Isolation: An individual failure does not abort processing for the whole batch
                _logger.LogError(ex, "[OutboxProcessor] Failed to dispatch OutboxMessage {MessageId}. Will retry on next polling cycle.", message.Id);
                message.MarkAsFailed(ex.Message);
            }
        }

        // 4. Save processed states back to database
        await dbContext.SaveChangesAsync(stoppingToken);
        return messages.Count;
    }
}
