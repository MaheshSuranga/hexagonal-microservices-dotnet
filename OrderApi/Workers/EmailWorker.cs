namespace OrderApi.Workers;

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderApi.Contracts.Events;

/// <summary>
/// Background worker simulating an Email Notification microservice.
/// Consumes OrderPlacedEvent from subscription 'sub-email-service'.
/// </summary>
public class EmailWorker : BackgroundService
{
    private readonly ServiceBusProcessor _processor;
    private readonly ILogger<EmailWorker> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EmailWorker(
        ServiceBusClient serviceBusClient,
        ILogger<EmailWorker> logger,
        string topicName = "order-placed-topic",
        string subscriptionName = "sub-email-service")
    {
        _logger = logger;

        // Configure ServiceBusProcessor with manual completion for reliability
        var options = new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 2,
            PrefetchCount = 5
        };

        _processor = serviceBusClient.CreateProcessor(topicName, subscriptionName, options);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        _logger.LogInformation("[EmailWorker] Starting subscription processor for 'sub-email-service'...");
        await _processor.StartProcessingAsync(stoppingToken);

        // Keep background service alive until host shutdown is signaled
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var messageId = args.Message.MessageId;
        var body = args.Message.Body.ToString();

        _logger.LogInformation("[EmailWorker] Processing message {MessageId} (DeliveryCount: {Count})",
            messageId, args.Message.DeliveryCount);

        try
        {
            var orderPlaced = JsonSerializer.Deserialize<OrderPlacedEvent>(body, JsonOptions);
            if (orderPlaced == null)
            {
                _logger.LogWarning("[EmailWorker] Malformed payload. Moving to Dead-Letter Queue (DLQ).");
                await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", "Payload could not be deserialized.", args.CancellationToken);
                return;
            }

            // Simulate email transmission latency (e.g. SendGrid / SMTP dispatch)
            await Task.Delay(300, args.CancellationToken);

            _logger.LogInformation(
                "📧 [EmailWorker] Order confirmation email dispatched to User '{UserId}' for Order '{OrderId}'. Total: {TotalAmount:C}",
                orderPlaced.UserId, orderPlaced.OrderId, orderPlaced.TotalAmount);

            // Complete the message: Removes it from the subscription
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (OperationCanceledException) when (args.CancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("[EmailWorker] Processing canceled during shutdown for message {MessageId}", messageId);
            await args.AbandonMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ [EmailWorker] Error processing message {MessageId}. DeliveryCount: {Count}",
                messageId, args.Message.DeliveryCount);

            // If threshold reached, dead-letter; otherwise abandon for retry
            if (args.Message.DeliveryCount >= 5)
            {
                _logger.LogError("[EmailWorker] Max delivery count exceeded. Dead-lettering message {MessageId}", messageId);
                await args.DeadLetterMessageAsync(args.Message, "MaxRetriesExceeded", ex.Message);
            }
            else
            {
                await args.AbandonMessageAsync(args.Message);
            }
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(args.Exception,
            "[EmailWorker] Error in ServiceBusProcessor: EntityPath '{Entity}', ErrorSource '{Source}'",
            args.EntityPath, args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("[EmailWorker] Gracefully stopping ServiceBusProcessor...");
        await _processor.StopProcessingAsync(cancellationToken);
        await _processor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
