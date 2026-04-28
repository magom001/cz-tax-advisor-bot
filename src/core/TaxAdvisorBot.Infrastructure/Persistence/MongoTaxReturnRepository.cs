using MongoDB.Driver;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Infrastructure.Persistence;

public sealed class MongoTaxReturnRepository : ITaxReturnRepository
{
    private readonly MongoCollections _collections;

    public MongoTaxReturnRepository(MongoCollections collections)
    {
        _collections = collections;
    }

    public async Task<TaxReturn?> GetAsync(string id, CancellationToken ct = default)
    {
        var doc = await _collections.TaxReturns.Find(t => t.Id == id).FirstOrDefaultAsync(ct);
        return doc?.Data;
    }

    public async Task<TaxReturn?> GetByYearAsync(int taxYear, CancellationToken ct = default)
    {
        var doc = await _collections.TaxReturns.Find(t => t.TaxYear == taxYear).FirstOrDefaultAsync(ct);
        return doc?.Data;
    }

    public async Task SaveAsync(TaxReturn taxReturn, CancellationToken ct = default)
    {
        var doc = new TaxReturnDocument
        {
            Id = taxReturn.Id,
            TaxYear = taxReturn.TaxYear,
            UpdatedAt = DateTime.UtcNow,
            Data = taxReturn
        };

        await _collections.TaxReturns.ReplaceOneAsync(
            t => t.Id == taxReturn.Id, doc,
            new ReplaceOptions { IsUpsert = true }, ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await _collections.TaxReturns.DeleteOneAsync(t => t.Id == id, ct);
    }

    public async Task<IReadOnlyList<TaxReturn>> GetAllAsync(CancellationToken ct = default)
    {
        var docs = await _collections.TaxReturns
            .Find(FilterDefinition<TaxReturnDocument>.Empty)
            .SortByDescending(t => t.TaxYear)
            .ToListAsync(ct);

        return docs.Select(d => d.Data).ToList();
    }
}
