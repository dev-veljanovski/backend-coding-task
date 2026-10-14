using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Threading.Channels;
using Claims.Domain.Abstractions;

namespace Claims.Infrastructure.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(typeof(TimeSpan), "00:00:00.100", "00:10:00", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 50;

    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 10;

    [Range(typeof(TimeSpan), "00:00:01", "01:00:00", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(5);

    [Range(typeof(TimeSpan), "00:00:05", "00:30:00", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(30);

    [Range(typeof(TimeSpan), "00:01:00", "365.00:00:00", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)]
    public TimeSpan ProcessedRetention { get; set; } = TimeSpan.FromDays(7);
}

internal interface IOutboxSignal
{
    void Notify();

    Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

// Channel used as a wake-up signal so new messages are picked up immediately instead of on the next poll.
internal sealed class OutboxSignal : IOutboxSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
    });

    public void Notify() => _channel.Writer.TryWrite(true);

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            await _channel.Reader.ReadAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }
}

internal sealed class DomainEventSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, Type> _typesByName;
    private readonly Dictionary<Type, string> _namesByType;

    public DomainEventSerializer()
    {
        var eventTypes = typeof(IDomainEvent).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(type))
            .ToList();

        _typesByName = eventTypes.ToDictionary(type => type.Name, StringComparer.Ordinal);
        _namesByType = eventTypes.ToDictionary(type => type, type => type.Name);
    }

    public (string Type, string Payload) Serialize(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var type = domainEvent.GetType();
        if (!_namesByType.TryGetValue(type, out var name))
        {
            throw new InvalidOperationException($"Domain event type '{type.FullName}' is not registered.");
        }

        return (name, JsonSerializer.Serialize(domainEvent, type, JsonOptions));
    }

    public IDomainEvent Deserialize(string typeName, string payload)
    {
        if (!_typesByName.TryGetValue(typeName, out var type))
        {
            throw new InvalidOperationException($"Unknown domain event type '{typeName}'.");
        }

        return (IDomainEvent)(JsonSerializer.Deserialize(payload, type, JsonOptions)
            ?? throw new InvalidOperationException($"Payload of '{typeName}' deserialized to null."));
    }
}
