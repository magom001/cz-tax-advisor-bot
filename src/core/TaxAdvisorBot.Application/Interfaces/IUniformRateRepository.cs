namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Repository for §38 uniform yearly exchange rates.
/// </summary>
public interface IUniformRateRepository
{
    Task<decimal?> GetRateAsync(int year, string currencyCode, CancellationToken ct = default);
    Task SetRateAsync(int year, string currencyCode, decimal rate, CancellationToken ct = default);
    Task<IReadOnlyList<UniformRateEntry>> GetAllAsync(CancellationToken ct = default);
}

public sealed record UniformRateEntry(int Year, string CurrencyCode, decimal Rate);
