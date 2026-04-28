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
    private readonly IExchangeRateService _exchangeRateService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DocumentExtractionJobHandler> _logger;

    public DocumentExtractionJobHandler(
        Kernel kernel,
        ContentExtractor contentExtractor,
        ITaxReturnRepository taxReturnRepo,
        IExchangeRateService exchangeRateService,
        INotificationService notificationService,
        ILogger<DocumentExtractionJobHandler> logger)
    {
        _kernel = kernel;
        _contentExtractor = contentExtractor;
        _taxReturnRepo = taxReturnRepo;
        _exchangeRateService = exchangeRateService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(DocumentUploadJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing document: {FileName} ({ContentType}, {Size} bytes)",
            job.FileName, job.ContentType, job.Data.Length);

        await _notificationService.SendProgressAsync(job.FileName,
            new ProgressUpdate("Extracting text", 10, $"Reading {job.FileName}..."), cancellationToken);

        // Extract text from the document
        var text = await ExtractTextAsync(job, cancellationToken);

        if (string.IsNullOrWhiteSpace(text) || text.Length < 50)
        {
            _logger.LogWarning("Document {FileName} produced insufficient text ({Length} chars)", job.FileName, text.Length);
            await _notificationService.SendProgressAsync(job.FileName,
                new ProgressUpdate("Failed", 100, $"Could not extract meaningful text from {job.FileName}"), cancellationToken);
            return;
        }

        _logger.LogInformation("Extracted {Length} chars from {FileName}", text.Length, job.FileName);

        await _notificationService.SendProgressAsync(job.FileName,
            new ProgressUpdate("Analyzing", 40, "Sending to AI for data extraction..."), cancellationToken);

        // Detect document type and extract structured data
        var result = await ExtractWithLlmAsync(text, job.FileName, cancellationToken);

        if (result is null)
        {
            await _notificationService.SendProgressAsync(job.FileName,
                new ProgressUpdate("Failed", 100, $"AI could not extract data from {job.FileName}"), cancellationToken);
            return;
        }

        await _notificationService.SendProgressAsync(job.FileName,
            new ProgressUpdate("Saving", 80, $"Extracted {result.TransactionCount} items from {job.FileName}"), cancellationToken);

        // Save to repository
        await ApplyExtractionResultAsync(result, cancellationToken);

        await _notificationService.SendProgressAsync(job.FileName,
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

        // Truncate very long documents to avoid context window issues
        if (text.Length > 50_000)
        {
            _logger.LogWarning("Document text truncated from {Original} to 50000 chars", text.Length);
            text = text[..50_000];
        }

        var history = new ChatHistory();
        history.AddSystemMessage(ExtractionPrompt);
        history.AddUserMessage($"File: {fileName}\n\n{text}");

        try
        {
            // Use streaming to avoid HttpClient timeout on large documents
            var sb = new StringBuilder();
            await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(history, cancellationToken: ct))
            {
                if (chunk.Content is not null)
                    sb.Append(chunk.Content);
            }

            var json = sb.ToString().Trim();

            _logger.LogDebug("LLM extraction response for {FileName}: {Response}", fileName,
                json.Length > 500 ? json[..500] + "..." : json);

            // Strip markdown fences
            if (json.StartsWith("```"))
                json = System.Text.RegularExpressions.Regex.Replace(json, @"```(?:json)?\s*([\s\S]*?)\s*```", "$1");

            return JsonSerializer.Deserialize<ExtractionResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse LLM extraction JSON for {FileName}", fileName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM extraction failed for {FileName}", fileName);
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
                var acquisitionDate = DateOnly.TryParse(tx.Date, out var date) ? date : DateOnly.FromDateTime(DateTime.Today);
                var currencyCode = tx.CurrencyCode ?? "USD";

                // Fetch ČNB exchange rate for the transaction date
                decimal? exchangeRate = null;
                try
                {
                    exchangeRate = await _exchangeRateService.GetDailyRateAsync(acquisitionDate, currencyCode, ct);
                    _logger.LogDebug("Fetched ČNB rate for {Date} {Currency}: {Rate}", acquisitionDate, currencyCode, exchangeRate);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch ČNB rate for {Date} {Currency}", acquisitionDate, currencyCode);
                }

                taxReturn.StockTransactions.Add(new StockTransaction
                {
                    TransactionType = Enum.TryParse<StockTransactionType>(tx.Type, true, out var type)
                        ? type : StockTransactionType.RsuVesting,
                    Ticker = tx.Ticker ?? "UNKNOWN",
                    Quantity = tx.Quantity ?? 0,
                    AcquisitionDate = acquisitionDate,
                    AcquisitionPricePerShare = tx.PricePerShare ?? 0,
                    SalePricePerShare = tx.SalePricePerShare,
                    EsppPurchasePricePerShare = tx.EsppPurchasePrice,
                    CurrencyCode = currencyCode,
                    ExchangeRate = exchangeRate,
                    BrokerName = tx.BrokerName,
                    TaxWithheldAbroad = tx.TaxWithheld ?? 0,
                    GrossAmount = tx.Amount,
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
          "taxYear": 2024,
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
              "date": "2024-03-15",
              "ticker": "MSFT",
              "quantity": 10,
              "pricePerShare": 420.72,
              "salePricePerShare": null,
              "esppPurchasePrice": 378.65,
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
        
        CRITICAL — TAX YEAR DETECTION:
        - The taxYear is the CALENDAR YEAR in which the transactions occurred.
        - Look at the statement period header (e.g. "January 1, 2024 - January 31, 2024" → taxYear = 2024).
        - Look at transaction dates. If all transactions are in 2024, taxYear = 2024.
        - Do NOT assume the current year. Read the actual dates from the document.
        
        CRITICAL — ESPP (Employee Stock Purchase Plan):
        - ESPP reports have a dedicated "Employee Stock Purchase Summary" section with these columns:
          * "Purchase Price" = the DISCOUNTED price the employee paid → put in "esppPurchasePrice"
          * "Purchase Date Fair Market Value" or "FMV" = the actual market price at purchase → put in "pricePerShare"
          * "Shares Purchased" = number of shares → put in "quantity"
          * "Gain from Purchase" = (FMV - Purchase Price) × shares (informational, do not extract)
        - The "pricePerShare" field MUST be the Fair Market Value (FMV), NOT the purchase price.
        - The "esppPurchasePrice" field MUST be the discounted purchase price.
        - Example: Purchase Price $378.65, FMV $420.72, Shares 2.408
          → pricePerShare: 420.72, esppPurchasePrice: 378.65, quantity: 2.408
        
        Rules:
        - Only extract these transaction types — IGNORE everything else:
          * RSU vesting ("Shares Deposited", "Vesting") → type "RsuVesting"
          * ESPP purchases (from the "Employee Stock Purchase Summary" section) → type "EsppDiscount"
          * Share sales (explicit sale with proceeds/gain/loss) → type "ShareSale"
          * Dividends (MSFT dividend payments) → type "Dividend"
          * Tax withheld on dividends → type "TaxWithheld"
        - IGNORE: internal cash adjustments, money market fund interest (e.g. "Fidelity Government Cash Reserves"),
          SPP purchase credits, core fund activity, small rounding amounts, and any transaction where ticker is unknown.
        - The ticker must be a real stock symbol (e.g. "MSFT"). If you cannot determine the ticker, skip the transaction.
        - "Shares Deposited" or "Vesting" = type "RsuVesting". Do NOT mark these as "ShareSale".
        - Only use "ShareSale" if the document explicitly shows shares being SOLD (proceeds, sale date, gain/loss).
        - For dividends, quantity and pricePerShare can be null — just set the amount.
        - For tax withheld on dividends, use negative amount and type "TaxWithheld".
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
    public decimal? Quantity { get; set; }
    public decimal? PricePerShare { get; set; }
    public decimal? SalePricePerShare { get; set; }
    public decimal? EsppPurchasePrice { get; set; }
    public decimal? Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? BrokerName { get; set; }
    public decimal? TaxWithheld { get; set; }
}

internal sealed class DeductionData
{
    public decimal PensionFund { get; set; }
    public decimal LifeInsurance { get; set; }
    public decimal MortgageInterest { get; set; }
    public decimal Donations { get; set; }
}
