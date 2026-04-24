namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Searches the vector database for relevant Czech tax law paragraphs.
/// </summary>
public interface ILegalSearchService
{
    /// <summary>
    /// Performs hybrid search (vector + keyword) for legal text relevant to the query.
    /// </summary>
    /// <param name="query">Natural language query or § reference.</param>
    /// <param name="effectiveYear">Tax year to filter results by.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked list of legal text chunks with metadata.</returns>
    Task<IReadOnlyList<LegalSearchResult>> SearchAsync(
        string query,
        int effectiveYear,
        int maxResults = 5,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A single result from the legal search service.
/// </summary>
public sealed record LegalSearchResult(
    string ParagraphId,
    string? SubParagraph,
    string TextContent,
    double RelevanceScore,
    string? SourceUrl);
