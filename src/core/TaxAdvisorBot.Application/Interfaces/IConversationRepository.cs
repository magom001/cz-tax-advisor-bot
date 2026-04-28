using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Repository for conversation history.
/// </summary>
public interface IConversationRepository
{
    Task<ConversationHistory?> GetAsync(string sessionId, CancellationToken ct = default);
    Task SaveAsync(ConversationHistory conversation, CancellationToken ct = default);
    Task AddMessageAsync(string sessionId, string role, string content, CancellationToken ct = default);
    Task DeleteAsync(string sessionId, CancellationToken ct = default);
}

public sealed class ConversationHistory
{
    public string SessionId { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ConversationMessage> Messages { get; set; } = [];
}

public sealed record ConversationMessage(string Role, string Content, DateTime Timestamp);
