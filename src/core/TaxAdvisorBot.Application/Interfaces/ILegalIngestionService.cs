namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Ingests legal text into the vector database for RAG search.
/// </summary>
public interface ILegalIngestionService
{
    /// <summary>
    /// Deletes and recreates the vector collection. Call before a full re-ingestion.
    /// </summary>
    Task ResetCollectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Scrapes a legal document from a URL, chunks it by § boundaries using LLM,
    /// generates embeddings, and stores in the vector DB.
    /// </summary>
    /// <param name="sourceUrl">URL of the legal text to ingest.</param>
    /// <param name="effectiveYear">Tax year this version of the law is effective for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of chunks ingested.</returns>
    Task<int> IngestFromUrlAsync(string sourceUrl, int effectiveYear, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests raw legal text, chunks by § boundaries, embeds, and stores.
    /// </summary>
    /// <param name="rawText">The full legal text content.</param>
    /// <param name="documentType">Type identifier (e.g. "Act", "Instruction").</param>
    /// <param name="effectiveYear">Tax year this version applies to.</param>
    /// <param name="sourceUrl">Source URL for citation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of chunks ingested.</returns>
    Task<int> IngestTextAsync(string rawText, string documentType, int effectiveYear, string? sourceUrl = null, CancellationToken cancellationToken = default);
}
