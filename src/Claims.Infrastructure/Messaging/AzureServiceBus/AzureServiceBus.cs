using System.Globalization;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Claims.Infrastructure.Messaging.AzureServiceBus;

internal static class ServiceBusClientFactory
{
    public static ServiceBusClient Create(IOptions<MessagingOptions> options)
    {
        var settings = options.Value.AzureServiceBus;

        return string.IsNullOrWhiteSpace(settings.ConnectionString)
            ? new ServiceBusClient(settings.FullyQualifiedNamespace, new DefaultAzureCredential())
            : new ServiceBusClient(settings.ConnectionString);
    }
}

internal sealed class AzureServiceBusMessageBus : IMessageBus, IAsyncDisposable
{
    private const string OccurredOnProperty = "occurredOnUtc";
    private const string TraceParentProperty = "traceparent";

    private readonly ServiceBusSender _sender;

    public AzureServiceBusMessageBus(ServiceBusClient client, IOptions<MessagingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        _sender = client.CreateSender(options.Value.AzureServiceBus.QueueName);
    }

    public Task PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var message = new ServiceBusMessage(BinaryData.FromString(envelope.Payload))
        {
            MessageId = envelope.MessageId.ToString(),
            Subject = envelope.MessageType,
            ContentType = "application/json",
        };
        message.ApplicationProperties[OccurredOnProperty] = envelope.OccurredOnUtc.ToString("O", CultureInfo.InvariantCulture);
        if (envelope.TraceParent is not null)
        {
            message.ApplicationProperties[TraceParentProperty] = envelope.TraceParent;
        }

        return _sender.SendMessageAsync(message, cancellationToken);
    }

    public ValueTask DisposeAsync() => _sender.DisposeAsync();

    internal static MessageEnvelope ToEnvelope(ServiceBusReceivedMessage message) => new(
        Guid.Parse(message.MessageId),
        message.Subject,
        message.Body.ToString(),
        message.ApplicationProperties.TryGetValue(OccurredOnProperty, out var occurredOn)
            ? DateTimeOffset.Parse((string)occurredOn, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            : message.EnqueuedTime,
        message.ApplicationProperties.TryGetValue(TraceParentProperty, out var traceParent) ? traceParent as string : null);
}

internal sealed partial class AzureServiceBusConsumer(
    ServiceBusClient client,
    IServiceScopeFactory scopeFactory,
    IOptions<MessagingOptions> options,
    ILogger<AzureServiceBusConsumer> logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value.AzureServiceBus;

        _processor = client.CreateProcessor(settings.QueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = settings.MaxConcurrentCalls,
        });
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken).ConfigureAwait(false);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        await _processor.StopProcessingAsync(CancellationToken.None).ConfigureAwait(false);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        if (_processor is not null)
        {
            await _processor.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var envelope = AzureServiceBusMessageBus.ToEnvelope(args.Message);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IncomingMessageDispatcher>();
        await dispatcher.DispatchAsync(envelope, args.CancellationToken).ConfigureAwait(false);

        await args.CompleteMessageAsync(args.Message, args.CancellationToken).ConfigureAwait(false);
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        LogProcessingError(logger, args.Exception, args.EntityPath, args.ErrorSource.ToString());
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Azure Service Bus error on {EntityPath} ({ErrorSource})")]
    private static partial void LogProcessingError(ILogger logger, Exception exception, string entityPath, string errorSource);
}
