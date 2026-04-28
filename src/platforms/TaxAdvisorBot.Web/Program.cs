using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TaxAdvisorBot.Application;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Application.Options;
using TaxAdvisorBot.Infrastructure;
using TaxAdvisorBot.Infrastructure.Search;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddApplicationOptions();
builder.AddInfrastructureServices();

var app = builder.Build();

// Track background ingestion jobs
var ingestionJobs = new ConcurrentDictionary<string, IngestionJob>();

app.MapDefaultEndpoints();
app.UseStaticFiles();

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
    Results.Ok(await repo.GetAllAsync(ct)));

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

class IngestionJob(string url)
{
    public string Url { get; } = url;
    public string Status { get; set; } = "queued";
    public int ChunksIngested { get; set; }
    public string? Error { get; set; }
}
