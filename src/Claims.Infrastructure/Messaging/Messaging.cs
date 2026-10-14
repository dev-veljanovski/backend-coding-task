using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Claims.Application.Abstractions.Events;
using Claims.Infrastructure.Diagnostics;
using Claims.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Claims.Infrastructure.Messaging;

internal sealed record MessageEnvelope(
    Guid MessageId,
    string MessageType,
    string Payload,
    DateTimeOffset OccurredOnUtc,
    string? TraceParent);

internal interface IMessageBus
{
    Task PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken);
}

public enum MessagingTransport
{
    InProcess = 0,

    AzureServiceBus = 1,
}

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public MessagingTransport Transport { get; set; } = MessagingTransport.InProcess;

    public AzureServiceBusOptions AzureServiceBus { get; set; } = new();
}

public sealed class AzureServiceBusOptions
{
    public string? ConnectionString { get; set; }

    public string? FullyQualifiedNamespace { get; set; }

    [Required]
    public string QueueName { get; set; } = "claims-domain-events";

    [Range(1, 64)]
    public int MaxConcurrentCalls { get; set; } = 4;
}

internal sealed class MessagingOptionsValidator : IValidateOptions<MessagingOptions>
{
    public ValidateOptionsResult Validate(string? name, MessagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Transport == MessagingTransport.AzureServiceBus
            && string.IsNullOrWhiteSpace(options.AzureServiceBus.ConnectionString)
            && string.IsNullOrWhiteSpace(options.AzureServiceBus.FullyQualifiedNamespace))
        {
            return ValidateOptionsResult.Fail(
                "Messaging:AzureServiceBus requires either a ConnectionString or a FullyQualifiedNamespace.");
        }

        return ValidateOptionsResult.Success;
    }
}

internal sealed class IncomingMessageDispatcher(DomainEventSerializer serializer, IDomainEventDispatcher dispatcher)
{
    public async Task DispatchAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        ActivityContext.TryParse(envelope.TraceParent, traceState: null, out var parentContext);
        using var activity = InfrastructureDiagnostics.ActivitySource.StartActivity(
            $"{envelope.MessageType} process",
            ActivityKind.Consumer,
            parentContext);
        activity?.SetTag("messaging.message.id", envelope.MessageId);

        var domainEvent = serializer.Deserialize(envelope.MessageType, envelope.Payload);
        var metadata = new EventMetadata(envelope.MessageId, envelope.OccurredOnUtc);

        await dispatcher.DispatchAsync(domainEvent, metadata, cancellationToken).ConfigureAwait(false);
    }
}

internal sealed class InProcessMessageBus(IServiceScopeFactory scopeFactory) : IMessageBus
{
    public async Task PublishAsync(MessageEnvelope envelope, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IncomingMessageDispatcher>();
        await dispatcher.DispatchAsync(envelope, cancellationToken).ConfigureAwait(false);
    }
}
