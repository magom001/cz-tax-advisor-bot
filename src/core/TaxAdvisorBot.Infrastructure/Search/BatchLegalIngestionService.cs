using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using TaxAdvisorBot.Application.Options;

namespace TaxAdvisorBot.Infrastructure.Search;

/// <summary>
/// Ingests all configured legal sources using Azure OpenAI Batch API (50% cheaper, no timeouts).
/// Scrapes all sources → builds JSONL → uploads → creates batch → polls → parses → embeds → stores in Qdrant.
/// Requires a GlobalBatch deployment in Azure AI Foundry.
/// </summary>
public sealed partial class BatchLegalIngestionService
{
    private const string ApiVersion = "2024-10-21";
    private const int MaxCharsPerBatch = 30_000;

    private static readonly string SystemPrompt = """
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
        """;

    private readonly HttpClient _httpClient;
    private readonly QdrantClient _qdrant;
    private readonly EmbeddingService _embeddingService;
    private readonly ContentExtractor _contentExtractor;
    private readonly AzureAIOptions _aiOptions;
    private readonly QdrantOptions _qdrantOptions;
    private readonly LegalSourcesOptions _sourcesOptions;
    private readonly ILogger<BatchLegalIngestionService> _logger;

    public BatchLegalIngestionService(
        HttpClient httpClient,
        QdrantClient qdrant,
        EmbeddingService embeddingService,
        ContentExtractor contentExtractor,
        IOptions<AzureAIOptions> aiOptions,
        IOptions<QdrantOptions> qdrantOptions,
        IOptions<LegalSourcesOptions> sourcesOptions,
        ILogger<BatchLegalIngestionService> logger)
    {
        _httpClient = httpClient;
        _qdrant = qdrant;
        _embeddingService = embeddingService;
        _contentExtractor = contentExtractor;
        _aiOptions = aiOptions.Value;
        _qdrantOptions = qdrantOptions.Value;
        _sourcesOptions = sourcesOptions.Value;
        _logger = logger;
    }

    private string BatchEndpoint => (_aiOptions.BatchEndpoint ?? _aiOptions.Endpoint).TrimEnd('/');
    private string BatchModel => _aiOptions.BatchDeploymentName ?? _aiOptions.FastChatDeploymentName;

    /// <summary>
    /// Current status of the batch ingestion process.
    /// </summary>
    public BatchIngestionStatus Status { get; private set; } = new();

    /// <summary>
    /// Runs the full batch ingestion pipeline for all configured sources.
    /// </summary>
    public async Task RunAsync(int effectiveYear, CancellationToken cancellationToken = default)
    {
        var sources = _sourcesOptions.Sources;
        if (sources.Count == 0)
        {
            _logger.LogWarning("No legal sources configured in appsettings");
            return;
        }

        try
        {
            // Step 1: Scrape all sources
            Status = new() { Phase = "Scraping", TotalSources = sources.Count };
            var scraped = await ScrapeAllAsync(sources, cancellationToken);

            // Step 2: Build JSONL
            Status.Phase = "Building JSONL";
            var (jsonl, taskMap) = BuildJsonl(scraped);
            _logger.LogInformation("Built JSONL with {Count} requests", taskMap.Count);
            Status.TotalBatchRequests = taskMap.Count;

            // Save JSONL locally for debugging
            var jsonlPath = Path.Combine(Path.GetTempPath(), "taxadvisor-batch.jsonl");
            await File.WriteAllTextAsync(jsonlPath, jsonl, cancellationToken);
            _logger.LogInformation("JSONL saved to {Path}", jsonlPath);

            // Step 3: Upload file
            Status.Phase = "Uploading file";
            var fileId = await UploadFileAsync(jsonl, cancellationToken);
            _logger.LogInformation("Uploaded batch file: {FileId}", fileId);

            // Step 4: Create batch
            Status.Phase = "Creating batch";
            var batchId = await CreateBatchAsync(fileId, cancellationToken);
            _logger.LogInformation("Created batch job: {BatchId}", batchId);
            Status.BatchId = batchId;

            // Step 5: Poll until complete
            Status.Phase = "Processing";
            var outputFileId = await PollBatchAsync(batchId, cancellationToken);
            _logger.LogInformation("Batch complete, output file: {FileId}", outputFileId);

            // Step 6: Download and parse results
            Status.Phase = "Downloading results";
            var chunks = await DownloadAndParseResultsAsync(outputFileId, taskMap, cancellationToken);
            _logger.LogInformation("Parsed {Count} legal chunks from batch results", chunks.Count);

            // Step 7: Embed and store
            Status.Phase = "Embedding & storing";
            Status.TotalChunks = chunks.Count;
            await EmbedAndStoreAsync(chunks, effectiveYear, cancellationToken);

            Status.Phase = "Done";
            _logger.LogInformation("Batch ingestion complete: {Count} chunks stored", chunks.Count);
        }
        catch (Exception ex)
        {
            Status.Phase = "Error";
            Status.Error = ex.Message;
            _logger.LogError(ex, "Batch ingestion failed");
            throw;
        }
    }

    private async Task<List<(LegalSource Source, string Text)>> ScrapeAllAsync(
        List<LegalSource> sources, CancellationToken ct)
    {
        var results = new List<(LegalSource, string)>();
        foreach (var source in sources)
        {
            try
            {
                _logger.LogInformation("Scraping {Name} from {Url}", source.Name, source.Url);
                Status.CurrentSource = source.Name;
                Status.ScrapedSources++;

                var response = await _httpClient.GetAsync(source.Url, ct);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Skipping {Name}: HTTP {Status}", source.Name, (int)response.StatusCode);
                    continue;
                }

                var text = await _contentExtractor.ExtractAsync(response, ct);

                if (text.Length < 100)
                {
                    _logger.LogWarning("Skipping {Name}: content too short ({Length} chars)", source.Name, text.Length);
                    continue;
                }

                results.Add((source, text));
                _logger.LogInformation("Scraped {Name}: {Length} chars", source.Name, text.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping {Name}: {Error}", source.Name, ex.Message);
            }
        }
        return results;
    }

    private (string Jsonl, Dictionary<string, TaskMeta> TaskMap) BuildJsonl(
        List<(LegalSource Source, string Text)> scraped)
    {
        var sb = new StringBuilder();
        var taskMap = new Dictionary<string, TaskMeta>();
        var taskId = 0;

        foreach (var (source, text) in scraped)
        {
            for (var offset = 0; offset < text.Length; offset += MaxCharsPerBatch)
            {
                var batch = text.Substring(offset, Math.Min(MaxCharsPerBatch, text.Length - offset));
                var customId = $"task-{taskId++}";

                taskMap[customId] = new TaskMeta(source.Name, source.Url, source.DocumentType);

                var request = new
                {
                    custom_id = customId,
                    method = "POST",
                    url = "/v1/chat/completions",
                    body = new
                    {
                        model = BatchModel,
                        messages = new object[]
                        {
                            new { role = "system", content = SystemPrompt },
                            new { role = "user", content = batch }
                        }
                    }
                };

                sb.AppendLine(JsonSerializer.Serialize(request, JsonOptions));
            }
        }

        return (sb.ToString(), taskMap);
    }

    private async Task<string> UploadFileAsync(string jsonlContent, CancellationToken ct)
    {
        var url = $"{BatchEndpoint}/openai/files?api-version={ApiVersion}";

        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(jsonlContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/jsonl");
        content.Add(fileContent, "file", "batch-ingestion.jsonl");
        content.Add(new StringContent("batch"), "purpose");

        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Add("api-key", _aiOptions.ApiKey);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<string> CreateBatchAsync(string inputFileId, CancellationToken ct)
    {
        var url = $"{BatchEndpoint}/openai/batches?api-version={ApiVersion}";

        var body = JsonSerializer.Serialize(new
        {
            input_file_id = inputFileId,
            endpoint = "/v1/chat/completions",
            completion_window = "24h"
        }, JsonOptions);

        _logger.LogInformation("Creating batch with body: {Body}", body);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("api-key", _aiOptions.ApiKey);

        var response = await _httpClient.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Batch creation failed: HTTP {Status} — {Body}", (int)response.StatusCode, json);
            throw new HttpRequestException($"Batch creation failed ({(int)response.StatusCode}): {json}");
        }

        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private async Task<string> PollBatchAsync(string batchId, CancellationToken ct)
    {
        var url = $"{BatchEndpoint}/openai/batches/{batchId}?api-version={ApiVersion}";

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("api-key", _aiOptions.ApiKey);

            var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var status = doc.RootElement.GetProperty("status").GetString();

            Status.BatchStatus = status;

            if (doc.RootElement.TryGetProperty("request_counts", out var counts))
            {
                Status.CompletedRequests = counts.TryGetProperty("completed", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0;
                Status.FailedRequests = counts.TryGetProperty("failed", out var f) && f.ValueKind == JsonValueKind.Number ? f.GetInt32() : 0;
            }

            _logger.LogInformation("Batch {BatchId} status: {Status}", batchId, status);

            switch (status)
            {
                case "completed":
                    return doc.RootElement.GetProperty("output_file_id").GetString()!;
                case "failed":
                case "cancelled":
                case "expired":
                    var errors = doc.RootElement.TryGetProperty("errors", out var e) ? e.ToString() : "unknown";
                    throw new InvalidOperationException($"Batch {status}: {errors}");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }

    private async Task<List<(TaskMeta Meta, LegalChunk Chunk)>> DownloadAndParseResultsAsync(
        string outputFileId, Dictionary<string, TaskMeta> taskMap, CancellationToken ct)
    {
        var url = $"{BatchEndpoint}/openai/files/{outputFileId}/content?api-version={ApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("api-key", _aiOptions.ApiKey);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(ct);
        var allChunks = new List<(TaskMeta, LegalChunk)>();

        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var doc = JsonDocument.Parse(line);
                var customId = doc.RootElement.GetProperty("custom_id").GetString()!;

                if (!taskMap.TryGetValue(customId, out var meta))
                    continue;

                var responseBody = doc.RootElement.GetProperty("response").GetProperty("body");
                var choices = responseBody.GetProperty("choices");
                var messageContent = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "[]";

                // Strip markdown fences
                messageContent = MarkdownJsonFenceRegex().Replace(messageContent, "$1");

                var chunks = JsonSerializer.Deserialize<List<LegalChunk>>(messageContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (chunks is not null)
                {
                    allChunks.AddRange(chunks.Select(c => (meta, c)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse batch result line");
            }
        }

        return allChunks;
    }

    private async Task EmbedAndStoreAsync(
        List<(TaskMeta Meta, LegalChunk Chunk)> chunks, int effectiveYear, CancellationToken ct)
    {
        await EnsureCollectionAsync(ct);

        var pointId = 0ul;
        try
        {
            var info = await _qdrant.GetCollectionInfoAsync(_qdrantOptions.CollectionName, ct);
            pointId = info.PointsCount;
        }
        catch { /* empty collection */ }

        for (var i = 0; i < chunks.Count; i++)
        {
            var (meta, chunk) = chunks[i];
            var textForEmbedding = $"[§{chunk.ParagraphId}] {chunk.Title}: {chunk.Content}";
            var embedding = await _embeddingService.GenerateEmbeddingAsync(textForEmbedding, ct);

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
                    ["document_type"] = meta.DocumentType,
                    ["source_url"] = meta.SourceUrl,
                    ["source_name"] = meta.SourceName,
                }
            };

            await _qdrant.UpsertAsync(_qdrantOptions.CollectionName, [point], cancellationToken: ct);
            Status.StoredChunks = i + 1;
        }
    }

    private async Task EnsureCollectionAsync(CancellationToken ct)
    {
        try
        {
            await _qdrant.GetCollectionInfoAsync(_qdrantOptions.CollectionName, ct);
        }
        catch
        {
            await _qdrant.CreateCollectionAsync(
                _qdrantOptions.CollectionName,
                new VectorParams { Size = (ulong)_qdrantOptions.VectorSize, Distance = Distance.Cosine },
                cancellationToken: ct);
        }
    }

    private static string StripHtml(string html)
    {
        var text = Regex.Replace(html, @"<(script|style)[^>]*>.*?</\1>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", " ");
        text = text.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    [GeneratedRegex(@"```(?:json)?\s*([\s\S]*?)\s*```")]
    private static partial Regex MarkdownJsonFenceRegex();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

internal sealed record TaskMeta(string SourceName, string SourceUrl, string DocumentType);

/// <summary>
/// Tracks progress of a batch ingestion run.
/// </summary>
public sealed class BatchIngestionStatus
{
    public string Phase { get; set; } = "Idle";
    public string? CurrentSource { get; set; }
    public int TotalSources { get; set; }
    public int ScrapedSources { get; set; }
    public int TotalBatchRequests { get; set; }
    public string? BatchId { get; set; }
    public string? BatchStatus { get; set; }
    public int CompletedRequests { get; set; }
    public int FailedRequests { get; set; }
    public int TotalChunks { get; set; }
    public int StoredChunks { get; set; }
    public string? Error { get; set; }
}
