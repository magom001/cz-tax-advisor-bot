using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using TaxAdvisorBot.Application;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Application.Options;
using TaxAdvisorBot.Domain.Enums;
using TaxAdvisorBot.Domain.Models;
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

// File upload (supports multiple files)
app.MapPost("/api/documents/upload", async (HttpRequest request, IJobQueue jobQueue, CancellationToken ct) =>
{
    const long maxSizePerFile = 50 * 1024 * 1024; // 50 MB per file
    var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg",
        "text/csv", "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    var form = await request.ReadFormAsync(ct);
    if (form.Files.Count == 0)
        return Results.BadRequest(new { error = "No files provided" });

    var results = new List<object>();
    var errors = new List<object>();

    foreach (var file in form.Files)
    {
        if (file.Length == 0)
        {
            errors.Add(new { fileName = file.FileName, error = "Empty file" });
            continue;
        }
        if (file.Length > maxSizePerFile)
        {
            errors.Add(new { fileName = file.FileName, error = $"Too large ({file.Length / 1024 / 1024} MB). Max {maxSizePerFile / 1024 / 1024} MB." });
            continue;
        }
        if (!allowedTypes.Contains(file.ContentType))
        {
            errors.Add(new { fileName = file.FileName, error = $"Unsupported type: {file.ContentType}" });
            continue;
        }

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);

        await jobQueue.EnqueueAsync(new DocumentUploadJob(
            FileName: file.FileName,
            ContentType: file.ContentType,
            Data: ms.ToArray()
        ), ct);

        results.Add(new { fileName = file.FileName, size = file.Length, status = "queued" });
    }

    return Results.Accepted(value: new { uploaded = results, errors });
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

// Output generation
app.MapGet("/api/output/table", async (int? year, ITaxReturnOutputService output, ITaxReturnRepository repo, CancellationToken ct) =>
{
    var taxYear = year ?? DateTime.Now.Year - 1;
    var taxReturn = await repo.GetByYearAsync(taxYear, ct);
    if (taxReturn is null) return Results.NotFound(new { error = $"No tax return for year {taxYear}" });
    var pdf = await output.GenerateCalculationTableAsync(taxReturn, ct);
    return Results.File(pdf, "application/pdf", $"stock-calculation-{taxYear}.pdf");
});

// Seed sample tax return for testing
app.MapPost("/api/test/seed", async (int? year, ITaxReturnRepository repo, CancellationToken ct) =>
{
    var taxYear = year ?? 2025;
    var taxReturn = new TaxReturn
    {
        TaxYear = taxYear,
        FirstName = "Jan",
        LastName = "Novák",
        PersonalIdNumber = "8503151234",
        DateOfBirth = new DateOnly(1985, 3, 15),
        Section6GrossIncome = 1_200_000m,
        Section6SocialInsurance = 396_000m,
        Section6HealthInsurance = 162_000m,
        Section6TaxWithheld = 180_000m,
        HasForeignIncome = true,
        ForeignIncomeCurrency = "USD",
        PensionFundContributions = 24_000m,
        MortgageInterestPaid = 80_000m,
        BasicTaxCredit = 30_840m,
        DependentChildrenCount = 2,
        ChildTaxBenefit = 30_408m,
        StockTransactions =
        [
            new StockTransaction
            {
                TransactionType = StockTransactionType.RsuVesting,
                Ticker = "MSFT",
                Quantity = 50,
                AcquisitionDate = new DateOnly(taxYear, 3, 15),
                AcquisitionPricePerShare = 420.50m,
                CurrencyCode = "USD",
                ExchangeRate = 23.15m,
                BrokerName = "Fidelity",
            },
            new StockTransaction
            {
                TransactionType = StockTransactionType.RsuVesting,
                Ticker = "MSFT",
                Quantity = 50,
                AcquisitionDate = new DateOnly(taxYear, 9, 15),
                AcquisitionPricePerShare = 445.00m,
                CurrencyCode = "USD",
                ExchangeRate = 22.80m,
                BrokerName = "Fidelity",
            },
            new StockTransaction
            {
                TransactionType = StockTransactionType.EsppDiscount,
                Ticker = "MSFT",
                Quantity = 30,
                AcquisitionDate = new DateOnly(taxYear, 6, 30),
                AcquisitionPricePerShare = 430.00m,
                EsppPurchasePricePerShare = 387.00m,
                CurrencyCode = "USD",
                ExchangeRate = 23.00m,
                BrokerName = "Fidelity",
            },
            new StockTransaction
            {
                TransactionType = StockTransactionType.ShareSale,
                Ticker = "MSFT",
                Quantity = 20,
                AcquisitionDate = new DateOnly(taxYear - 4, 3, 15),
                SaleDate = new DateOnly(taxYear, 11, 10),
                AcquisitionPricePerShare = 280.00m,
                SalePricePerShare = 450.00m,
                CurrencyCode = "USD",
                ExchangeRate = 23.30m,
                BrokerName = "Fidelity",
            },
            new StockTransaction
            {
                TransactionType = StockTransactionType.ShareSale,
                Ticker = "MSFT",
                Quantity = 10,
                AcquisitionDate = new DateOnly(taxYear - 1, 9, 15),
                SaleDate = new DateOnly(taxYear, 11, 10),
                AcquisitionPricePerShare = 400.00m,
                SalePricePerShare = 450.00m,
                CurrencyCode = "USD",
                ExchangeRate = 23.30m,
                BrokerName = "Fidelity",
            },
        ]
    };

    await repo.SaveAsync(taxReturn, ct);
    return Results.Ok(new
    {
        message = $"Seeded tax return for {taxYear}",
        transactions = taxReturn.StockTransactions.Count,
        grossIncome = taxReturn.TotalGrossIncome
    });
});

app.MapGet("/api/output/all", async (int? year, ITaxReturnOutputService output, ITaxReturnRepository repo, CancellationToken ct) =>
{
    var taxYear = year ?? DateTime.Now.Year - 1;
    var taxReturn = await repo.GetByYearAsync(taxYear, ct);
    if (taxReturn is null) return Results.NotFound(new { error = $"No tax return for year {taxYear}" });
    var zip = await output.GenerateAllAsync(taxReturn, ct);
    return Results.File(zip, "application/zip", $"tax-return-{taxYear}.zip");
});

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

// Debug: list all tax returns (summary)
app.MapGet("/api/taxreturns", async (ITaxReturnRepository repo, CancellationToken ct) =>
{
    var all = await repo.GetAllAsync(ct);
    return Results.Ok(all.Select(t => new
    {
        t.Id, t.TaxYear, t.Status, t.FirstName, t.LastName,
        StockTransactionCount = t.StockTransactions.Count,
        t.TotalGrossIncome
    }));
});

// Debug: full tax return by year (with all transactions)
app.MapGet("/api/taxreturns/{year:int}", async (int year, ITaxReturnRepository repo, CancellationToken ct) =>
{
    var taxReturn = await repo.GetByYearAsync(year, ct);
    if (taxReturn is null) return Results.NotFound(new { error = $"No tax return for year {year}" });
    return Results.Ok(taxReturn);
});

// Debug: delete tax return by year
app.MapDelete("/api/taxreturns/{year:int}", async (int year, ITaxReturnRepository repo, CancellationToken ct) =>
{
    var taxReturn = await repo.GetByYearAsync(year, ct);
    if (taxReturn is null) return Results.NotFound(new { error = $"No tax return for year {year}" });
    await repo.DeleteAsync(taxReturn.Id, ct);
    return Results.Ok(new { message = $"Deleted tax return for year {year}" });
});

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
