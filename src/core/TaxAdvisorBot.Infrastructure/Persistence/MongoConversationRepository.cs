using MongoDB.Driver;
using TaxAdvisorBot.Application.Interfaces;

namespace TaxAdvisorBot.Infrastructure.Persistence;

public sealed class MongoConversationRepository : IConversationRepository
{
    private readonly MongoCollections _collections;

    public MongoConversationRepository(MongoCollections collections)
    {
        _collections = collections;
    }

    public async Task<ConversationHistory?> GetAsync(string sessionId, CancellationToken ct = default)
    {
        var doc = await _collections.Conversations.Find(c => c.Id == sessionId).FirstOrDefaultAsync(ct);
        if (doc is null) return null;

        return new ConversationHistory
        {
            SessionId = doc.Id,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt,
            Messages = doc.Messages.Select(m =>
                new ConversationMessage(m.Role, m.Content, m.Timestamp)).ToList()
        };
    }

    public async Task SaveAsync(ConversationHistory conversation, CancellationToken ct = default)
    {
        var doc = new ConversationDocument
        {
            Id = conversation.SessionId,
            CreatedAt = conversation.CreatedAt,
            UpdatedAt = DateTime.UtcNow,
            Messages = conversation.Messages.Select(m =>
                new ChatMessageDocument
                {
                    Role = m.Role,
                    Content = m.Content,
                    Timestamp = m.Timestamp
                }).ToList()
        };

        await _collections.Conversations.ReplaceOneAsync(
            c => c.Id == conversation.SessionId, doc,
            new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task AddMessageAsync(string sessionId, string role, string content, CancellationToken ct = default)
    {
        var message = new ChatMessageDocument
        {
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        };

        var update = Builders<ConversationDocument>.Update
            .Push(c => c.Messages, message)
            .Set(c => c.UpdatedAt, DateTime.UtcNow)
            .SetOnInsert(c => c.CreatedAt, DateTime.UtcNow);

        await _collections.Conversations.UpdateOneAsync(
            c => c.Id == sessionId, update,
            new UpdateOptions { IsUpsert = true }, ct);
    }

    public async Task DeleteAsync(string sessionId, CancellationToken ct = default)
    {
        await _collections.Conversations.DeleteOneAsync(c => c.Id == sessionId, ct);
    }
}
