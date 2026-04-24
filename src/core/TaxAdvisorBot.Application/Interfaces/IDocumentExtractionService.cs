using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Extracts structured data from uploaded tax documents using AI.
/// </summary>
public interface IDocumentExtractionService
{
    /// <summary>
    /// Extracts structured fields from a document stream.
    /// </summary>
    /// <param name="fileStream">The document file stream.</param>
    /// <param name="fileName">Original file name (used for format detection).</param>
    /// <param name="contentType">MIME content type of the document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extracted document context with confidence scores.</returns>
    Task<TaxDocumentContext> ExtractAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);
}
