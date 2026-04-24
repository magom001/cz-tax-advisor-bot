using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Application.Options;

namespace TaxAdvisorBot.Infrastructure.Search;

/// <summary>
/// Searches Czech tax law in Qdrant using hybrid search: vector similarity + keyword/metadata filtering.
/// </summary>
public sealed partial class QdrantLegalSearchService : ILegalSearchService
{
    private const double MinScoreThreshold = 0.65;

    private readonly QdrantClient _qdrant;
    private readonly EmbeddingService _embeddingService;
    private readonly QdrantOptions _options;
    private readonly ILogger<QdrantLegalSearchService> _logger;

    public QdrantLegalSearchService(
        QdrantClient qdrant,
        EmbeddingService embeddingService,
        IOptions<QdrantOptions> options,
        ILogger<QdrantLegalSearchService> logger)
    {
        _qdrant = qdrant;
        _embeddingService = embeddingService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<LegalSearchResult>> SearchAsync(
        string query,
        int effectiveYear,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching legal text: query={Query}, year={Year}", query, effectiveYear);

        var embedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        // Build filter: always filter by effective_year
        var filter = new Filter();
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = "effective_year",
                Match = new Qdrant.Client.Grpc.Match { Integer = effectiveYear }
            }
        });

        // If the query contains a § reference, add keyword filter
        var paragraphMatch = ParagraphRegex().Match(query);
        if (paragraphMatch.Success)
        {
            filter.Must.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "paragraph_id",
                    Match = new Qdrant.Client.Grpc.Match { Keyword = paragraphMatch.Groups[1].Value }
                }
            });
        }

        var results = await _qdrant.SearchAsync(
            collectionName: _options.CollectionName,
            vector: embedding.ToArray(),
            filter: filter,
            limit: (ulong)maxResults,
            scoreThreshold: (float)MinScoreThreshold,
            cancellationToken: cancellationToken);

        var searchResults = results
            .Select(r => new LegalSearchResult(
                ParagraphId: GetPayloadString(r, "paragraph_id") ?? "",
                SubParagraph: GetPayloadString(r, "sub_paragraph"),
                TextContent: GetPayloadString(r, "text_content") ?? "",
                RelevanceScore: r.Score,
                SourceUrl: GetPayloadString(r, "source_url")))
            .ToList();

        _logger.LogInformation("Found {Count} results above threshold {Threshold}", searchResults.Count, MinScoreThreshold);

        return searchResults;
    }

    private static string? GetPayloadString(ScoredPoint point, string key)
    {
        return point.Payload.TryGetValue(key, out var value) ? value.StringValue : null;
    }

    [GeneratedRegex(@"§\s*(\w+)", RegexOptions.Compiled)]
    private static partial Regex ParagraphRegex();
}
