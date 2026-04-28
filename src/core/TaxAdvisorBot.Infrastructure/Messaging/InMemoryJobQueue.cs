using System.Threading.Channels;
using TaxAdvisorBot.Application.Interfaces;

namespace TaxAdvisorBot.Infrastructure.Messaging;

/// <summary>
/// In-memory job queue using System.Threading.Channels.
/// Suitable for single-instance development. Swap for RabbitMQ/Azure Service Bus in production.
/// </summary>
public sealed class InMemoryJobQueue : IJobQueue
{
    private readonly Channel<JobEnvelope> _channel = Channel.CreateUnbounded<JobEnvelope>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    public async Task EnqueueAsync<T>(T job, CancellationToken cancellationToken = default) where T : class
    {
        var envelope = new JobEnvelope(typeof(T).FullName ?? typeof(T).Name, job);
        await _channel.Writer.WriteAsync(envelope, cancellationToken);
    }

    public async Task<T> DequeueAsync<T>(CancellationToken cancellationToken = default) where T : class
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            if (_channel.Reader.TryRead(out var envelope) && envelope.Payload is T typed)
            {
                return typed;
            }
        }

        throw new OperationCanceledException();
    }

    /// <summary>
    /// Reads the next available job regardless of type. Used by the processor.
    /// </summary>
    internal async Task<JobEnvelope> ReadAsync(CancellationToken cancellationToken)
    {
        return await _channel.Reader.ReadAsync(cancellationToken);
    }

    /// <summary>
    /// Whether there are pending jobs.
    /// </summary>
    internal bool TryPeek() => _channel.Reader.TryPeek(out _);
}

/// <summary>
/// Wraps a job with its type name for routing to the correct handler.
/// </summary>
internal sealed record JobEnvelope(string TypeName, object Payload);
