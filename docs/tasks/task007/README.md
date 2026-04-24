# Task 007 — Async Job Queue & Notification Service

## Objective

Implement the pub/sub job queue abstraction and the notification service interface, with an in-memory default implementation.

## Work Items

1. Implement `InMemoryJobQueue : IJobQueue`:
   - Uses `System.Threading.Channels` for in-process pub/sub.
   - `EnqueueAsync<T>(T job)` and `DequeueAsync<T>()` methods.
   - Support multiple job types (document processing, vectorization).
2. Create a background `JobProcessorService : BackgroundService`:
   - Listens on the channel, dispatches jobs to registered handlers.
   - Calls `INotificationService.SendProgressAsync` during processing.
   - Calls `INotificationService.SendCompletionAsync` when done.
3. Define `IJobHandler<T>` interface for job-specific processing logic.
4. Register in DI as singleton (channel) + hosted service (processor).
5. Write unit tests:
   - Enqueue a job, verify it is dequeued and processed.
   - Verify progress notifications are sent during processing.
   - Verify completion notification is sent after processing.

## Expected Results

- Jobs can be enqueued from any platform and processed asynchronously.
- Progress updates flow through `INotificationService`.
- The in-memory implementation works for single-instance development.
- Unit tests pass with mocked `INotificationService`.
- Architecture is ready to swap for RabbitMQ/Azure Service Bus later.
