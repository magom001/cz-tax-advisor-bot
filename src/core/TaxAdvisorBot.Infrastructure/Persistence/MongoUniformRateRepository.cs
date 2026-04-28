using MongoDB.Driver;
using TaxAdvisorBot.Application.Interfaces;

namespace TaxAdvisorBot.Infrastructure.Persistence;

public sealed class MongoUniformRateRepository : IUniformRateRepository
{
    private readonly MongoCollections _collections;

    public MongoUniformRateRepository(MongoCollections collections)
    {
        _collections = collections;
    }

    public async Task<decimal?> GetRateAsync(int year, string currencyCode, CancellationToken ct = default)
    {
        var id = BuildId(year, currencyCode);
        var doc = await _collections.UniformRates.Find(r => r.Id == id).FirstOrDefaultAsync(ct);
        return doc?.Rate;
    }

    public async Task SetRateAsync(int year, string currencyCode, decimal rate, CancellationToken ct = default)
    {
        var id = BuildId(year, currencyCode);
        var doc = new UniformRateDocument
        {
            Id = id,
            Year = year,
            CurrencyCode = currencyCode.ToUpperInvariant(),
            Rate = rate
        };

        await _collections.UniformRates.ReplaceOneAsync(
            r => r.Id == id, doc,
            new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task<IReadOnlyList<UniformRateEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var docs = await _collections.UniformRates
            .Find(FilterDefinition<UniformRateDocument>.Empty)
            .SortBy(r => r.Year).ThenBy(r => r.CurrencyCode)
            .ToListAsync(ct);

        return docs.Select(d => new UniformRateEntry(d.Year, d.CurrencyCode, d.Rate)).ToList();
    }

    private static string BuildId(int year, string currencyCode) =>
        $"{year}:{currencyCode.ToUpperInvariant()}";
}
