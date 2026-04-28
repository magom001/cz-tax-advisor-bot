using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TaxAdvisorBot.Infrastructure.Messaging;

/// <summary>
/// Background service that dequeues jobs from the InMemoryJobQueue and dispatches them to registered handlers.
/// </summary>
public sealed class JobProcessorService : BackgroundService
{
    private readonly InMemoryJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobProcessorService> _logger;

    public JobProcessorService(
        InMemoryJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<JobProcessorService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var envelope = await _queue.ReadAsync(stoppingToken);

                _logger.LogInformation("Processing job: {TypeName}", envelope.TypeName);

                await using var scope = _scopeFactory.CreateAsyncScope();

                // Find handler by convention: IJobHandler<T>
                var handlerType = typeof(IJobHandler<>).MakeGenericType(envelope.Payload.GetType());
                var handler = scope.ServiceProvider.GetService(handlerType);

                if (handler is null)
                {
                    _logger.LogWarning("No handler registered for job type {TypeName}", envelope.TypeName);
                    continue;
                }

                // Invoke HandleAsync via reflection (necessary for generic dispatch)
                var method = handlerType.GetMethod("HandleAsync")!;
                var task = (Task)method.Invoke(handler, [envelope.Payload, stoppingToken])!;
                await task;

                _logger.LogInformation("Job completed: {TypeName}", envelope.TypeName);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Job processing failed");
            }
        }

        _logger.LogInformation("Job processor stopped");
    }
}

/// <summary>
/// Handler for a specific job type. Register in DI to process jobs of type T.
/// </summary>
public interface IJobHandler<in T> where T : class
{
    Task HandleAsync(T job, CancellationToken cancellationToken = default);
}
