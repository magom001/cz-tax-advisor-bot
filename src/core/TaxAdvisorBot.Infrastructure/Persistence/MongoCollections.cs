using MongoDB.Driver;

namespace TaxAdvisorBot.Infrastructure.Persistence;

/// <summary>
/// Provides typed access to MongoDB collections. Used by repository implementations.
/// </summary>
public sealed class MongoCollections
{
    private readonly IMongoDatabase _database;

    public MongoCollections(IMongoDatabase database)
    {
        _database = database;
    }

    public IMongoCollection<UniformRateDocument> UniformRates =>
        _database.GetCollection<UniformRateDocument>("uniform_rates");

    public IMongoCollection<ConversationDocument> Conversations =>
        _database.GetCollection<ConversationDocument>("conversations");

    public IMongoCollection<TaxReturnDocument> TaxReturns =>
        _database.GetCollection<TaxReturnDocument>("tax_returns");
}

// ── MongoDB documents (internal storage format) ──

public sealed class UniformRateDocument
{
    public string Id { get; set; } = null!;  // "2024:USD"
    public int Year { get; set; }
    public string CurrencyCode { get; set; } = null!;
    public decimal Rate { get; set; }
}

public sealed class ConversationDocument
{
    public string Id { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessageDocument> Messages { get; set; } = [];
}

public sealed class ChatMessageDocument
{
    public string Role { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class TaxReturnDocument
{
    public string Id { get; set; } = null!;
    public int TaxYear { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Domain.Models.TaxReturn Data { get; set; } = null!;
}
