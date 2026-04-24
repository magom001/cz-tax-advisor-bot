using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Manages multi-turn AI chat conversations with the tax advisor.
/// </summary>
public interface IConversationService
{
    /// <summary>
    /// Sends a user message and streams the AI response.
    /// </summary>
    /// <param name="sessionId">Unique session identifier for conversation context.</param>
    /// <param name="message">The user's message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async stream of response chunks, followed by the final ChatResponse.</returns>
    IAsyncEnumerable<string> ChatAsync(
        string sessionId,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the final structured response for the last message in a session.
    /// </summary>
    Task<ChatResponse> GetLastResponseAsync(string sessionId, CancellationToken cancellationToken = default);
}
