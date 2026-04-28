using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Infrastructure.Messaging;

namespace TaxAdvisorBot.Infrastructure.Tests;

public sealed class InMemoryJobQueueTests
{
    [Fact]
    public async Task EnqueueAndDequeue_ReturnsJob()
    {
        var queue = new InMemoryJobQueue();
        var job = new TestJob("hello");

        await queue.EnqueueAsync(job);
        var result = await queue.DequeueAsync<TestJob>();

        Assert.Equal("hello", result.Message);
    }

    [Fact]
    public async Task DequeueAsync_BlocksUntilJobAvailable()
    {
        var queue = new InMemoryJobQueue();
        var cts = new CancellationTokenSource();

        // Start dequeue in background — will block
        var dequeueTask = queue.DequeueAsync<TestJob>(cts.Token);

        // Not completed yet
        Assert.False(dequeueTask.IsCompleted);

        // Now enqueue
        await queue.EnqueueAsync(new TestJob("delayed"));

        var result = await dequeueTask;
        Assert.Equal("delayed", result.Message);
    }

    [Fact]
    public async Task DequeueAsync_ThrowsOnCancellation()
    {
        var queue = new InMemoryJobQueue();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            queue.DequeueAsync<TestJob>(cts.Token));
    }

    [Fact]
    public async Task MultipleJobs_DequeuedInOrder()
    {
        var queue = new InMemoryJobQueue();

        await queue.EnqueueAsync(new TestJob("first"));
        await queue.EnqueueAsync(new TestJob("second"));
        await queue.EnqueueAsync(new TestJob("third"));

        Assert.Equal("first", (await queue.DequeueAsync<TestJob>()).Message);
        Assert.Equal("second", (await queue.DequeueAsync<TestJob>()).Message);
        Assert.Equal("third", (await queue.DequeueAsync<TestJob>()).Message);
    }

}

public sealed record TestJob(string Message);
