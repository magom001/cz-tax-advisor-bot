using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Application.Options;

namespace TaxAdvisorBot.Infrastructure.Search;

/// <summary>
/// Scrapes Czech tax law, uses LLM to extract and chunk § paragraphs,
/// generates embeddings, and stores in Qdrant.
/// </summary>
public sealed partial class LegalIngestionService : ILegalIngestionService
{
    private readonly HttpClient _httpClient;
    private readonly QdrantClient _qdrant;
    private readonly EmbeddingService _embeddingService;
    private readonly ContentExtractor _contentExtractor;
    private readonly Kernel _kernel;
    private readonly QdrantOptions _options;
    private readonly ILogger<LegalIngestionService> _logger;

    public LegalIngestionService(
        HttpClient httpClient,
        QdrantClient qdrant,
        EmbeddingService embeddingService,
        ContentExtractor contentExtractor,
        Kernel kernel,
        IOptions<QdrantOptions> options,
        ILogger<LegalIngestionService> logger)
    {
        _httpClient = httpClient;
        _qdrant = qdrant;
        _embeddingService = embeddingService;
        _contentExtractor = contentExtractor;
        _kernel = kernel;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ResetCollectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _qdrant.DeleteCollectionAsync(_options.CollectionName);
            _logger.LogInformation("Deleted collection '{Collection}'", _options.CollectionName);
        }
        catch
        {
            // Collection didn't exist
        }

        await _qdrant.CreateCollectionAsync(
            _options.CollectionName,
            new VectorParams { Size = (ulong)_options.VectorSize, Distance = Distance.Cosine });
        _logger.LogInformation("Created fresh collection '{Collection}'", _options.CollectionName);
    }

    public async Task<int> IngestFromUrlAsync(string sourceUrl, int effectiveYear, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scraping legal text from {Url}", sourceUrl);

        var response = await _httpClient.GetAsync(sourceUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        var rawText = await _contentExtractor.ExtractAsync(response, cancellationToken);

        return await IngestTextAsync(rawText, "Act", effectiveYear, sourceUrl, cancellationToken);
    }

    public async Task<int> IngestTextAsync(string rawText, string documentType, int effectiveYear, string? sourceUrl = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ingesting legal text: type={Type}, year={Year}, length={Length}",
            documentType, effectiveYear, rawText.Length);

        // Ensure collection exists
        await EnsureCollectionAsync(cancellationToken);

        // Use LLM to chunk the text into § paragraphs
        var chunks = await ChunkWithLlmAsync(rawText, cancellationToken);

        _logger.LogInformation("LLM extracted {Count} chunks from legal text", chunks.Count);

        var pointId = 0ul;

        // Get existing max point ID to avoid collisions
        try
        {
            var collectionInfo = await _qdrant.GetCollectionInfoAsync(_options.CollectionName, cancellationToken);
            pointId = collectionInfo.PointsCount;
        }
        catch
        {
            // Collection might be empty
        }

        foreach (var chunk in chunks)
        {
            var textForEmbedding = $"[§{chunk.ParagraphId}] {chunk.Title}: {chunk.Content}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(textForEmbedding, cancellationToken);

            pointId++;
            var point = new PointStruct
            {
                Id = new PointId { Num = pointId },
                Vectors = embedding.ToArray(),
                Payload =
                {
                    ["paragraph_id"] = chunk.ParagraphId,
                    ["sub_paragraph"] = chunk.SubParagraph ?? "",
                    ["text_content"] = chunk.Content,
                    ["title"] = chunk.Title,
                    ["effective_year"] = effectiveYear,
                    ["document_type"] = documentType,
                    ["source_url"] = sourceUrl ?? "",
                }
            };

            await _qdrant.UpsertAsync(_options.CollectionName, [point], cancellationToken: cancellationToken);

            _logger.LogDebug("Stored chunk §{ParagraphId} (point {PointId})", chunk.ParagraphId, pointId);
        }

        _logger.LogInformation("Ingestion complete: {Count} chunks stored in collection '{Collection}'",
            chunks.Count, _options.CollectionName);

        return chunks.Count;
    }

    private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _qdrant.GetCollectionInfoAsync(_options.CollectionName, cancellationToken);
            _logger.LogDebug("Collection '{Collection}' already exists", _options.CollectionName);
        }
        catch
        {
            _logger.LogInformation("Creating collection '{Collection}' with vector size {Size}",
                _options.CollectionName, _options.VectorSize);

            await _qdrant.CreateCollectionAsync(
                _options.CollectionName,
                new VectorParams { Size = (ulong)_options.VectorSize, Distance = Distance.Cosine },
                cancellationToken: cancellationToken);
        }
    }

    private async Task<IReadOnlyList<LegalChunk>> ChunkWithLlmAsync(string rawText, CancellationToken cancellationToken)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>("fast-chat");

        // If text is very long, process in batches
        const int maxCharsPerBatch = 30_000;
        var allChunks = new List<LegalChunk>();
        var batchCount = (int)Math.Ceiling((double)rawText.Length / maxCharsPerBatch);

        for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            var offset = batchIndex * maxCharsPerBatch;
            var batch = rawText.Substring(offset, Math.Min(maxCharsPerBatch, rawText.Length - offset));

            _logger.LogInformation("Processing batch {Batch}/{Total} (offset {Offset})", batchIndex + 1, batchCount, offset);

            var history = new ChatHistory();
            history.AddSystemMessage("""
                You are a legal text parser for Czech tax law. Extract each § (paragraph) from the provided text.
                
                Return a JSON array where each element has:
                - "paragraphId": the paragraph number (e.g. "10", "38f")
                - "subParagraph": sub-paragraph reference if applicable (e.g. "odst. 1 písm. b"), or null
                - "title": the section title/heading in Czech
                - "content": the full text of that paragraph
                
                Rules:
                - Each § should be a separate chunk
                - Keep the original Czech text, do not translate
                - Include all sub-paragraphs within the parent § chunk
                - If a paragraph has multiple sub-sections, include them all in the content
                - Return ONLY the JSON array, no markdown fences
                """);
            history.AddUserMessage(batch);

            // Use streaming to avoid HttpClient timeout — tokens arrive incrementally
            var sb = new StringBuilder();
            await foreach (var update in chatService.GetStreamingChatMessageContentsAsync(
                history, cancellationToken: cancellationToken))
            {
                if (update.Content is not null)
                    sb.Append(update.Content);
            }

            var json = sb.ToString().Trim();
            // Strip markdown code fences if present
            json = MarkdownJsonFenceRegex().Replace(json, "$1");

            _logger.LogDebug("Batch {Batch} response: {Length} chars", batchIndex + 1, json.Length);

            try
            {
                var chunks = JsonSerializer.Deserialize<List<LegalChunk>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (chunks is not null)
                {
                    allChunks.AddRange(chunks);
                    _logger.LogInformation("Batch {Batch}/{Total}: extracted {Count} chunks", batchIndex + 1, batchCount, chunks.Count);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse LLM chunk response for batch {Batch} at offset {Offset}", batchIndex + 1, offset);
            }
        }

        return allChunks;
    }

    private static string StripHtml(string html)
    {
        // Remove script/style blocks
        var text = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        // Remove HTML tags
        text = Regex.Replace(text, @"<[^>]+>", " ");
        // Decode common entities
        text = text.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        // Collapse whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)\s*```")]
    private static partial Regex MarkdownJsonFenceRegex();
}

internal sealed record LegalChunk(
    string ParagraphId,
    string? SubParagraph,
    string Title,
    string Content);
