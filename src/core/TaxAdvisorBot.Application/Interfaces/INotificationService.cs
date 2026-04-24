using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Pushes real-time progress updates and completions to connected clients.
/// Each platform (Web/SignalR, Telegram, CLI) provides its own implementation.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a progress update during a long-running operation.
    /// </summary>
    Task SendProgressAsync(string sessionId, ProgressUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the final completion response to the client.
    /// </summary>
    Task SendCompletionAsync(string sessionId, ChatResponse response, CancellationToken cancellationToken = default);
}
