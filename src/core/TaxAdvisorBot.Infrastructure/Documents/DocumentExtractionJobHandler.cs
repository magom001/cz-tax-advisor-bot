using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Domain.Enums;
using TaxAdvisorBot.Domain.Models;
using TaxAdvisorBot.Infrastructure.Messaging;
using TaxAdvisorBot.Infrastructure.Search;

namespace TaxAdvisorBot.Infrastructure.Documents;

/// <summary>
/// Processes uploaded user documents (brokerage PDFs, employment confirmations, etc.)
/// by extracting text and using gpt-4.1-mini to parse structured data.
/// </summary>
public sealed class DocumentExtractionJobHandler : IJobHandler<DocumentUploadJob>
{
    private readonly Kernel _kernel;
    private readonly ContentExtractor _contentExtractor;
    private readonly ITaxReturnRepository _taxReturnRepo;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DocumentExtractionJobHandler> _logger;

    public DocumentExtractionJobHandler(
        Kernel kernel,
        ContentExtractor contentExtractor,
        ITaxReturnRepository taxReturnRepo,
        INotificationService notificationService,
        ILogger<DocumentExtractionJobHandler> logger)
    {
        _kernel = kernel;
        _contentExtractor = contentExtractor;
        _taxReturnRepo = taxReturnRepo;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(DocumentUploadJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing document: {FileName} ({ContentType}, {Size} bytes)",
            job.FileName, job.ContentType, job.Data.Length);

        await _notificationService.SendProgressAsync("document",
            new ProgressUpdate("Extracting text", 10, $"Reading {job.FileName}..."), cancellationToken);

        // Extract text from the document
        var text = await ExtractTextAsync(job, cancellationToken);

        if (string.IsNullOrWhiteSpace(text) || text.Length < 50)
        {
            _logger.LogWarning("Document {FileName} produced insufficient text ({Length} chars)", job.FileName, text.Length);
            await _notificationService.SendProgressAsync("document",
                new ProgressUpdate("Failed", 100, $"Could not extract meaningful text from {job.FileName}"), cancellationToken);
            return;
        }

        _logger.LogInformation("Extracted {Length} chars from {FileName}", text.Length, job.FileName);

        await _notificationService.SendProgressAsync("document",
            new ProgressUpdate("Analyzing", 40, "Sending to AI for data extraction..."), cancellationToken);

        // Detect document type and extract structured data
        var result = await ExtractWithLlmAsync(text, job.FileName, cancellationToken);

        if (result is null)
        {
            await _notificationService.SendProgressAsync("document",
                new ProgressUpdate("Failed", 100, $"AI could not extract data from {job.FileName}"), cancellationToken);
            return;
        }

        await _notificationService.SendProgressAsync("document",
            new ProgressUpdate("Saving", 80, $"Extracted {result.TransactionCount} items from {job.FileName}"), cancellationToken);

        // Save to repository
        await ApplyExtractionResultAsync(result, cancellationToken);

        await _notificationService.SendProgressAsync("document",
            new ProgressUpdate("Done", 100, $"Processed {job.FileName}: {result.Summary}"), cancellationToken);

        _logger.LogInformation("Document {FileName} processed: {Summary}", job.FileName, result.Summary);
    }

    private async Task<string> ExtractTextAsync(DocumentUploadJob job, CancellationToken ct)
    {
        // Create a fake HttpResponseMessage to reuse ContentExtractor
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(job.Data)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(job.ContentType);

        return await _contentExtractor.ExtractAsync(response, ct);
    }

    private async Task<ExtractionResult?> ExtractWithLlmAsync(string text, string fileName, CancellationToken ct)
    {
        var chatService = _kernel.GetRequiredService<IChatCompletionService>("fast-chat");

        var history = new ChatHistory();
        history.AddSystemMessage(ExtractionPrompt);
        history.AddUserMessage($"File: {fileName}\n\n{text}");

        try
        {
            var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
            var json = response.Content?.Trim() ?? "";

            // Strip markdown fences
            if (json.StartsWith("```"))
                json = System.Text.RegularExpressions.Regex.Replace(json, @"```(?:json)?\s*([\s\S]*?)\s*```", "$1");

            return JsonSerializer.Deserialize<ExtractionResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM extraction response for {FileName}", fileName);
            return null;
        }
    }

    private async Task ApplyExtractionResultAsync(ExtractionResult result, CancellationToken ct)
    {
        // Get or create the tax return for the detected year
        var taxYear = result.TaxYear > 0 ? result.TaxYear : DateTime.Now.Year - 1;
        var taxReturn = await _taxReturnRepo.GetByYearAsync(taxYear, ct) ?? new TaxReturn { TaxYear = taxYear };

        // Apply employment data
        if (result.Employment is not null)
        {
            taxReturn.Section6GrossIncome = result.Employment.GrossIncome;
            taxReturn.Section6SocialInsurance = result.Employment.SocialInsurance;
            taxReturn.Section6HealthInsurance = result.Employment.HealthInsurance;
            taxReturn.Section6TaxWithheld = result.Employment.TaxWithheld;
        }

        // Apply stock transactions
        if (result.StockTransactions is { Count: > 0 })
        {
            foreach (var tx in result.StockTransactions)
            {
                taxReturn.StockTransactions.Add(new StockTransaction
                {
                    TransactionType = Enum.TryParse<StockTransactionType>(tx.Type, true, out var type)
                        ? type : StockTransactionType.RsuVesting,
                    Ticker = tx.Ticker ?? "UNKNOWN",
                    Quantity = tx.Quantity,
                    AcquisitionDate = DateOnly.TryParse(tx.Date, out var date) ? date : DateOnly.FromDateTime(DateTime.Today),
                    AcquisitionPricePerShare = tx.PricePerShare,
                    SalePricePerShare = tx.SalePricePerShare,
                    EsppPurchasePricePerShare = tx.EsppPurchasePrice,
                    CurrencyCode = tx.CurrencyCode ?? "USD",
                    BrokerName = tx.BrokerName,
                    TaxWithheldAbroad = tx.TaxWithheld,
                });
            }
        }

        // Apply deductions
        if (result.Deductions is not null)
        {
            if (result.Deductions.PensionFund > 0) taxReturn.PensionFundContributions = result.Deductions.PensionFund;
            if (result.Deductions.LifeInsurance > 0) taxReturn.LifeInsuranceContributions = result.Deductions.LifeInsurance;
            if (result.Deductions.MortgageInterest > 0) taxReturn.MortgageInterestPaid = result.Deductions.MortgageInterest;
            if (result.Deductions.Donations > 0) taxReturn.CharitableDonations = result.Deductions.Donations;
        }

        await _taxReturnRepo.SaveAsync(taxReturn, ct);
    }

    private const string ExtractionPrompt = """
        You are a document parser for Czech tax filing. Extract structured data from the uploaded document.
        
        Return a JSON object with these fields (include only what's found in the document):
        
        {
          "taxYear": 2025,
          "documentType": "BrokerageStatement" | "EmploymentConfirmation" | "PensionFundStatement" | "LifeInsuranceStatement" | "MortgageConfirmation" | "DonationReceipt",
          "employment": {
            "grossIncome": 0,
            "socialInsurance": 0,
            "healthInsurance": 0,
            "taxWithheld": 0
          },
          "stockTransactions": [
            {
              "type": "RsuVesting" | "EsppDiscount" | "ShareSale" | "Dividend" | "TaxWithheld",
              "date": "2025-03-15",
              "ticker": "MSFT",
              "quantity": 10,
              "pricePerShare": 400.00,
              "salePricePerShare": null,
              "esppPurchasePrice": null,
              "amount": 4000.00,
              "currencyCode": "USD",
              "brokerName": "Fidelity",
              "taxWithheld": 0
            }
          ],
          "deductions": {
            "pensionFund": 0,
            "lifeInsurance": 0,
            "mortgageInterest": 0,
            "donations": 0
          },
          "summary": "Brief description of what was extracted"
        }
        
        Rules:
        - Extract ALL transactions found in the document, not just the first one.
        - For brokerage statements, identify RSU vesting, ESPP purchases, dividends, and taxes withheld.
        - Amounts should be in the original currency (usually USD for US brokers).
        - Dates in yyyy-MM-dd format.
        - Return ONLY the JSON, no markdown fences or explanations.
        """;
}

// ── Extraction result DTOs ──

internal sealed class ExtractionResult
{
    public int TaxYear { get; set; }
    public string? DocumentType { get; set; }
    public EmploymentData? Employment { get; set; }
    public List<StockTransactionData>? StockTransactions { get; set; }
    public DeductionData? Deductions { get; set; }
    public string? Summary { get; set; }

    public int TransactionCount =>
        (StockTransactions?.Count ?? 0) +
        (Employment is not null ? 1 : 0) +
        (Deductions is not null ? 1 : 0);
}

internal sealed class EmploymentData
{
    public decimal GrossIncome { get; set; }
    public decimal SocialInsurance { get; set; }
    public decimal HealthInsurance { get; set; }
    public decimal TaxWithheld { get; set; }
}

internal sealed class StockTransactionData
{
    public string? Type { get; set; }
    public string? Date { get; set; }
    public string? Ticker { get; set; }
    public decimal Quantity { get; set; }
    public decimal PricePerShare { get; set; }
    public decimal? SalePricePerShare { get; set; }
    public decimal? EsppPurchasePrice { get; set; }
    public decimal Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? BrokerName { get; set; }
    public decimal TaxWithheld { get; set; }
}

internal sealed class DeductionData
{
    public decimal PensionFund { get; set; }
    public decimal LifeInsurance { get; set; }
    public decimal MortgageInterest { get; set; }
    public decimal Donations { get; set; }
}
