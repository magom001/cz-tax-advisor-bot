using System.IO.Compression;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Infrastructure.Output;

/// <summary>
/// Generates tax filing output artifacts from a completed TaxReturn.
/// </summary>
public sealed class TaxReturnOutputService : ITaxReturnOutputService
{
    private readonly StockCalculationTableGenerator _tableGenerator;

    public TaxReturnOutputService(StockCalculationTableGenerator tableGenerator)
    {
        _tableGenerator = tableGenerator;
    }

    public Task<byte[]> GenerateCalculationTableAsync(TaxReturn taxReturn, CancellationToken ct = default)
    {
        var pdf = _tableGenerator.Generate(taxReturn);
        return Task.FromResult(pdf);
    }

    public Task<byte[]> GenerateXmlAsync(TaxReturn taxReturn, CancellationToken ct = default)
    {
        // TODO: Task 015b — EPO XML generation from XSD schema
        throw new NotImplementedException("EPO XML generation not yet implemented. Requires XSD schema download (task 015b).");
    }

    public Task<byte[]> GeneratePdfAsync(TaxReturn taxReturn, CancellationToken ct = default)
    {
        // TODO: Task 015c — DPFO PDF form generation
        throw new NotImplementedException("DPFO PDF declaration not yet implemented (task 015c).");
    }

    public async Task<byte[]> GenerateAllAsync(TaxReturn taxReturn, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Calculation table (always available)
            var table = await GenerateCalculationTableAsync(taxReturn, ct);
            var tableEntry = zip.CreateEntry($"stock-calculation-{taxReturn.TaxYear}.pdf");
            await using (var stream = tableEntry.Open())
                await stream.WriteAsync(table, ct);

            // XML (if implemented)
            try
            {
                var xml = await GenerateXmlAsync(taxReturn, ct);
                var xmlEntry = zip.CreateEntry($"dpfo-{taxReturn.TaxYear}.xml");
                await using (var stream = xmlEntry.Open())
                    await stream.WriteAsync(xml, ct);
            }
            catch (NotImplementedException) { /* skip */ }

            // PDF declaration (if implemented)
            try
            {
                var pdf = await GeneratePdfAsync(taxReturn, ct);
                var pdfEntry = zip.CreateEntry($"dpfo-{taxReturn.TaxYear}.pdf");
                await using (var stream = pdfEntry.Open())
                    await stream.WriteAsync(pdf, ct);
            }
            catch (NotImplementedException) { /* skip */ }
        }

        return ms.ToArray();
    }
}
