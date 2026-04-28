using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Generates tax filing output artifacts from a completed TaxReturn.
/// </summary>
public interface ITaxReturnOutputService
{
    /// <summary>Generates the EPO XML for upload to the Financial Administration portal.</summary>
    Task<byte[]> GenerateXmlAsync(TaxReturn taxReturn, CancellationToken ct = default);

    /// <summary>Generates a PDF tax declaration (DPFO form).</summary>
    Task<byte[]> GeneratePdfAsync(TaxReturn taxReturn, CancellationToken ct = default);

    /// <summary>Generates a stock compensation calculation table (RSU/ESPP/dividends with ČNB rates).</summary>
    Task<byte[]> GenerateCalculationTableAsync(TaxReturn taxReturn, CancellationToken ct = default);

    /// <summary>Generates all artifacts bundled as a ZIP.</summary>
    Task<byte[]> GenerateAllAsync(TaxReturn taxReturn, CancellationToken ct = default);
}
