using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Deterministic tax calculation service. All math happens here — never in the LLM.
/// </summary>
public interface ITaxCalculationService
{
    /// <summary>
    /// Calculates tax for §10 other income (share sales, crypto, occasional income).
    /// </summary>
    Task<TaxCalculationResult> CalculateSection10TaxAsync(decimal income, decimal expenses, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculates the full DPFO tax liability from a completed TaxReturn.
    /// </summary>
    Task<TaxCalculationResult> CalculateFullTaxAsync(TaxReturn taxReturn, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a tax calculation with the computed amount and legal citations.
/// </summary>
public sealed record TaxCalculationResult(
    decimal TaxAmount,
    IReadOnlyList<LegalReference> Citations);
