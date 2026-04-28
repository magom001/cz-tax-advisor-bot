using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TaxAdvisorBot.Application;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Application.Options;
using TaxAdvisorBot.Infrastructure;
using TaxAdvisorBot.Infrastructure.Search;
using TaxAdvisorBot.Web.Hubs;
using TaxAdvisorBot.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationOptions();
builder.AddInfrastructureServices();

// SignalR for real-time chat
builder.Services.AddSignalR();
builder.Services.AddSingleton<INotificationService, SignalRNotificationService>();

var app = builder.Build();

// Track background ingestion jobs
var ingestionJobs = new ConcurrentDictionary<string, IngestionJob>();

app.MapDefaultEndpoints();
app.UseStaticFiles();

// SignalR hub
app.MapHub<ChatHub>("/hubs/chat");

// File upload
app.MapPost("/api/documents/upload", async (IFormFile file, IJobQueue jobQueue, CancellationToken ct) =>
{
    const long maxSize = 10 * 1024 * 1024; // 10 MB
    var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg",
        "text/csv", "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    if (file.Length == 0)
        return Results.BadRequest(new { error = "Empty file" });

    if (file.Length > maxSize)
        return Results.BadRequest(new { error = $"File too large. Maximum {maxSize / 1024 / 1024} MB." });

    if (!allowedTypes.Contains(file.ContentType))
        return Results.BadRequest(new { error = $"Unsupported file type: {file.ContentType}" });

    // Read file into memory and enqueue for processing
    using var ms = new MemoryStream();
    await file.CopyToAsync(ms, ct);

    await jobQueue.EnqueueAsync(new DocumentUploadJob(
        FileName: file.FileName,
        ContentType: file.ContentType,
        Data: ms.ToArray()
    ), ct);

    return Results.Accepted(value: new { fileName = file.FileName, size = file.Length, status = "queued" });
}).DisableAntiforgery();

// Chat API — streaming tax advisor (SSE fallback for non-SignalR clients)
app.MapGet("/api/chat", async (string message, string? sessionId, IConversationService chat, HttpContext ctx, CancellationToken ct) =>
{
    var sid = sessionId ?? Guid.NewGuid().ToString("N")[..8];

    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";

    await foreach (var chunk in chat.ChatAsync(sid, message, ct))
    {
        await ctx.Response.WriteAsync($"data: {chunk.Replace("\n", "\\n")}\n\n", ct);
        await ctx.Response.Body.FlushAsync(ct);
    }

    await ctx.Response.WriteAsync($"event: done\ndata: {sid}\n\n", ct);
    await ctx.Response.Body.FlushAsync(ct);
});

// Sources catalog from appsettings
app.MapGet("/api/sources", (IOptions<LegalSourcesOptions> options) =>
    Results.Ok(options.Value.Sources));

// Search API
app.MapGet("/api/search", async (string q, int? year, ILegalSearchService search, CancellationToken ct) =>
{
    var results = await search.SearchAsync(q, year ?? DateTime.Now.Year, cancellationToken: ct);
    return Results.Ok(results);
});

// Uniform exchange rates
app.MapGet("/api/rates", async (IUniformRateRepository repo, CancellationToken ct) =>
{
    try
    {
        var rates = await repo.GetAllAsync(ct);
        return Results.Ok(rates);
    }
    catch (Exception ex)
    {
        return Results.Ok(Array.Empty<object>()); // Return empty list if MongoDB not ready
    }
});

app.MapPost("/api/rates", async (UniformRateRequest req, IUniformRateRepository repo, CancellationToken ct) =>
{
    await repo.SetRateAsync(req.Year, req.CurrencyCode, req.Rate, ct);
    return Results.Ok(new { message = $"Set {req.Year}:{req.CurrencyCode} = {req.Rate}" });
});

// Reset the Qdrant collection (wipe before re-ingestion)
app.MapPost("/api/ingest/reset", async (ILegalIngestionService ingestion, CancellationToken ct) =>
{
    await ingestion.ResetCollectionAsync(ct);
    return Results.Ok(new { message = "Collection reset" });
});

// Start ingestion as background job — returns immediately
app.MapPost("/api/ingest", (IngestRequest request, ILegalIngestionService ingestion) =>
{
    var jobId = Guid.NewGuid().ToString("N")[..8];
    var job = new IngestionJob(request.Url);
    ingestionJobs[jobId] = job;

    _ = Task.Run(async () =>
    {
        try
        {
            job.Status = "ingesting";
            var count = await ingestion.IngestFromUrlAsync(request.Url, request.Year);
            job.ChunksIngested = count;
            job.Status = "done";
        }
        catch (Exception ex)
        {
            job.Status = "error";
            job.Error = ex.Message;
        }
    });

    return Results.Accepted($"/api/ingest/{jobId}", new { jobId });
});

// Poll ingestion job status
app.MapGet("/api/ingest/{jobId}", (string jobId) =>
{
    if (!ingestionJobs.TryGetValue(jobId, out var job))
        return Results.NotFound();
    return Results.Ok(new { job.Url, job.Status, job.ChunksIngested, job.Error });
});

app.MapFallbackToFile("index.html");

// Batch ingestion: ingest all configured sources via Azure OpenAI Batch API
app.MapPost("/api/batch-ingest", (BatchIngestRequest request, BatchLegalIngestionService batchService) =>
{
    _ = Task.Run(async () =>
    {
        await batchService.RunAsync(request.Year);
    });

    return Results.Accepted("/api/batch-ingest/status", new { message = "Batch ingestion started" });
});

app.MapGet("/api/batch-ingest/status", (BatchLegalIngestionService batchService) =>
    Results.Ok(batchService.Status));

app.Run();

record IngestRequest(string Url, int Year);
record BatchIngestRequest(int Year);
record UniformRateRequest(int Year, string CurrencyCode, decimal Rate);
record DocumentUploadJob(string FileName, string ContentType, byte[] Data);

class IngestionJob(string url)
{
    public string Url { get; } = url;
    public string Status { get; set; } = "queued";
    public int ChunksIngested { get; set; }
    public string? Error { get; set; }
}
