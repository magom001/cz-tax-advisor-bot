using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Data;
using TaxAdvisorBot.Application.Interfaces;

#pragma warning disable SKEXP0001 // ITextSearch is experimental
#pragma warning disable CS0618    // ITextSearch is obsolete in favor of ITextSearch<T>

namespace TaxAdvisorBot.Infrastructure.Search;

/// <summary>
/// Adapts our ILegalSearchService to Semantic Kernel's ITextSearch interface,
/// enabling the TextSearchProvider to use our existing Qdrant-based search.
/// </summary>
public sealed class LegalTextSearchAdapter : ITextSearch
{
    private readonly ILegalSearchService _searchService;
    private readonly ILogger<LegalTextSearchAdapter> _logger;

    public LegalTextSearchAdapter(ILegalSearchService searchService, ILogger<LegalTextSearchAdapter> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    public async Task<KernelSearchResults<string>> SearchAsync(
        string query,
        TextSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var top = options?.Top ?? 5;
        _logger.LogInformation("RAG SearchAsync called: query='{Query}', top={Top}", query, top);
        var results = await _searchService.SearchAsync(query, DateTime.Now.Year, top, cancellationToken);
        _logger.LogInformation("RAG SearchAsync returned {Count} results", results.Count);

        async IAsyncEnumerable<string> GetResults()
        {
            foreach (var r in results)
            {
                yield return $"[§{r.ParagraphId}] {r.TextContent}";
                await Task.CompletedTask;
            }
        }

        return new KernelSearchResults<string>(GetResults());
    }

    public async Task<KernelSearchResults<TextSearchResult>> GetTextSearchResultsAsync(
        string query,
        TextSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var top = options?.Top ?? 5;
        _logger.LogInformation("RAG GetTextSearchResultsAsync called: query='{Query}', top={Top}", query, top);
        var results = await _searchService.SearchAsync(query, DateTime.Now.Year, top, cancellationToken);
        _logger.LogInformation("RAG GetTextSearchResultsAsync returned {Count} results", results.Count);

        async IAsyncEnumerable<TextSearchResult> GetResults()
        {
            foreach (var r in results)
            {
                yield return new TextSearchResult(r.TextContent)
                {
                    Name = $"§{r.ParagraphId}{(r.SubParagraph is not null ? " " + r.SubParagraph : "")}",
                    Link = r.SourceUrl,
                };
                await Task.CompletedTask;
            }
        }

        return new KernelSearchResults<TextSearchResult>(GetResults());
    }

    public async Task<KernelSearchResults<object>> GetSearchResultsAsync(
        string query,
        TextSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var top = options?.Top ?? 5;
        var results = await _searchService.SearchAsync(query, DateTime.Now.Year, top, cancellationToken);

        async IAsyncEnumerable<object> GetResults()
        {
            foreach (var r in results)
            {
                yield return r;
                await Task.CompletedTask;
            }
        }

        return new KernelSearchResults<object>(GetResults());
    }
}
