using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Repository for persisted tax return state.
/// </summary>
public interface ITaxReturnRepository
{
    Task<TaxReturn?> GetAsync(string id, CancellationToken ct = default);
    Task<TaxReturn?> GetByYearAsync(int taxYear, CancellationToken ct = default);
    Task SaveAsync(TaxReturn taxReturn, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<TaxReturn>> GetAllAsync(CancellationToken ct = default);
}
