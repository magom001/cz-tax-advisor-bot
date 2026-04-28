using TaxAdvisorBot.Domain.Enums;

namespace TaxAdvisorBot.Domain.Models;

/// <summary>
/// Represents a single stock-related income event (RSU vest, ESPP purchase, or share sale).
/// Used to calculate taxable income with the 3-year exemption rule for share sales.
/// </summary>
public sealed record StockTransaction
{
    /// <summary>Type of stock event.</summary>
    public required StockTransactionType TransactionType { get; init; }

    /// <summary>Ticker symbol (e.g. "MSFT", "GOOG").</summary>
    public required string Ticker { get; init; }

    /// <summary>Number of shares involved.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Date the shares were acquired (vest date for RSU, purchase date for ESPP/buy).</summary>
    public required DateOnly AcquisitionDate { get; init; }

    /// <summary>Date the shares were sold. Null if not yet sold (RSU vest or ESPP with no sale).</summary>
    public DateOnly? SaleDate { get; init; }

    /// <summary>Fair market value per share at acquisition (in original currency).</summary>
    public required decimal AcquisitionPricePerShare { get; init; }

    /// <summary>Sale price per share (in original currency). Null if not sold.</summary>
    public decimal? SalePricePerShare { get; init; }

    /// <summary>ESPP purchase price per share (the discounted price). Only for ESPP transactions.</summary>
    public decimal? EsppPurchasePricePerShare { get; init; }

    /// <summary>ISO 4217 currency code (e.g. "USD").</summary>
    public required string CurrencyCode { get; init; }

    /// <summary>ČNB exchange rate used for CZK conversion (CZK per 1 unit of foreign currency).</summary>
    public decimal? ExchangeRate { get; init; }

    /// <summary>Broker or plan name (e.g. "E*TRADE", "Fidelity", "Charles Schwab").</summary>
    public string? BrokerName { get; init; }

    /// <summary>Tax already withheld abroad on this transaction.</summary>
    public decimal TaxWithheldAbroad { get; set; }

    /// <summary>Gross amount for dividends or tax withheld (where quantity × price doesn't apply).</summary>
    public decimal? GrossAmount { get; init; }

    // ── Computed properties ──

    /// <summary>
    /// Whether this share sale is exempt from capital gains tax (held &gt; 3 years under §4 odst. 1 písm. w).
    /// Only applicable to ShareSale transactions.
    /// </summary>
    public bool IsExemptFromTax =>
        TransactionType == StockTransactionType.ShareSale
        && SaleDate.HasValue
        && SaleDate.Value > AcquisitionDate.AddYears(3);

    /// <summary>
    /// Total acquisition cost in original currency (quantity × acquisition price per share).
    /// </summary>
    public decimal TotalAcquisitionCost => Quantity * AcquisitionPricePerShare;

    /// <summary>
    /// Total sale proceeds in original currency. Zero if not sold.
    /// </summary>
    public decimal TotalSaleProceeds => SalePricePerShare.HasValue ? Quantity * SalePricePerShare.Value : 0m;

    /// <summary>
    /// Capital gain/loss in original currency. Only meaningful for ShareSale.
    /// </summary>
    public decimal CapitalGain => TotalSaleProceeds - TotalAcquisitionCost;

    /// <summary>
    /// ESPP discount amount per share (FMV - purchase price). Only for ESPP transactions.
    /// </summary>
    public decimal EsppDiscountPerShare =>
        EsppPurchasePricePerShare.HasValue
            ? AcquisitionPricePerShare - EsppPurchasePricePerShare.Value
            : 0m;

    /// <summary>
    /// Total ESPP discount (taxable as §6 employment income).
    /// </summary>
    public decimal TotalEsppDiscount => Quantity * EsppDiscountPerShare;
}
