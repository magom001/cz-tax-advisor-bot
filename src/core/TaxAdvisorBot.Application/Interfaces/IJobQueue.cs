namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Pub/sub job queue for async background processing.
/// Default: in-memory channels. Production: RabbitMQ or Azure Service Bus.
/// </summary>
public interface IJobQueue
{
    /// <summary>
    /// Enqueues a job for background processing.
    /// </summary>
    Task EnqueueAsync<T>(T job, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Dequeues the next available job. Blocks until a job is available or cancellation.
    /// </summary>
    Task<T> DequeueAsync<T>(CancellationToken cancellationToken = default) where T : class;
}
