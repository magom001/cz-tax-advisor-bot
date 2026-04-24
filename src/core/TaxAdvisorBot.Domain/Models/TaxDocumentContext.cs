using TaxAdvisorBot.Domain.Enums;

namespace TaxAdvisorBot.Domain.Models;

/// <summary>
/// Structured data extracted from an uploaded tax document.
/// Populated by the document extraction service.
/// </summary>
public sealed class TaxDocumentContext
{
    /// <summary>Type of the extracted document.</summary>
    public DocumentType DocumentType { get; set; } = DocumentType.Unknown;

    /// <summary>Original file name of the uploaded document.</summary>
    public string? FileName { get; set; }

    /// <summary>The tax section this document is relevant to.</summary>
    public TaxSection? RelevantSection { get; set; }

    /// <summary>Primary income amount extracted from the document.</summary>
    public decimal? IncomeAmount { get; set; }

    /// <summary>Expense or deduction amount extracted from the document.</summary>
    public decimal? ExpenseAmount { get; set; }

    /// <summary>Tax withheld or paid, as stated in the document.</summary>
    public decimal? TaxWithheld { get; set; }

    /// <summary>Currency code if foreign document (ISO 4217).</summary>
    public string? CurrencyCode { get; set; }

    /// <summary>Date of the transaction or period covered.</summary>
    public DateOnly? DocumentDate { get; set; }

    /// <summary>Tax year the document relates to.</summary>
    public int? TaxYear { get; set; }

    /// <summary>Extraction confidence score (0.0–1.0). Values below threshold should be flagged for review.</summary>
    public double ConfidenceScore { get; set; }

    /// <summary>Raw extracted text or notes from the extraction service.</summary>
    public string? RawText { get; set; }
}
