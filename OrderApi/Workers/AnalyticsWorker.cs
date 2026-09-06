namespace OrderApi.Workers;

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderApi.Contracts.Events;

/// <summary>
/// Background worker simulating an Analytics & Reporting microservice.
/// Consumes OrderPlacedEvent from subscription 'sub-analytics-service'.
/// </summary>
public class AnalyticsWorker : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly ILogger<AnalyticsWorker> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AnalyticsWorker(
        ServiceBusClient serviceBusClient,
        ILogger<AnalyticsWorker> logger,
        string topicName = "order-placed-topic",
        string subscriptionName = "sub-analytics-service")
    {
        _logger = logger;

        var options = new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 2,
            PrefetchCount = 10
        };

        _processor = serviceBusClient.CreateProcessor(topicName, subscriptionName, options);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        _logger.LogInformation("[AnalyticsWorker] Starting subscription processor for 'sub-analytics-service'...");
        await _processor.StartProcessingAsync(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var messageId = args.Message.MessageId;
        var body = args.Message.Body.ToString();

        try
        {
            var orderPlaced = JsonSerializer.Deserialize<OrderPlacedEvent>(body, JsonOptions);
            if (orderPlaced == null)
            {
                await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", "Unable to deserialize OrderPlacedEvent.");
                return;
            }

            // Simulate analytical data aggregation (e.g. TimescaleDB / ClickHouse / Data Lake ingestion)
            _logger.LogInformation(
                "📊 [AnalyticsWorker] Ingested revenue metric: Order '{OrderId}', User '{UserId}', Amount: {Amount:C}, Timestamp: {Timestamp:O}",
                orderPlaced.OrderId, orderPlaced.UserId, orderPlaced.TotalAmount, orderPlaced.OccurredAtUtc);

            // Complete message
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("[AnalyticsWorker] Processing canceled during shutdown for message {MessageId}", messageId);
            await args.AbandonMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [AnalyticsWorker] Error processing analytics event {MessageId}", messageId);
            await args.AbandonMessageAsync(args.Message);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "[AnalyticsWorker] Error in ServiceBusProcessor: EntityPath '{Entity}', ErrorSource '{Source}'",
            args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[AnalyticsWorker] Gracefully stopping ServiceBusProcessor...");
        await _processor.StopProcessingAsync(cancellationToken);
        await _processor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
