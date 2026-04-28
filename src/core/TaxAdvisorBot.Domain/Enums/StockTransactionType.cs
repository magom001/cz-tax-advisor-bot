namespace TaxAdvisorBot.Domain.Enums;

/// <summary>
/// Type of stock-related income event.
/// </summary>
public enum StockTransactionType
{
    /// <summary>RSU (Restricted Stock Unit) vesting — taxed as §6 employment income at vest date FMV.</summary>
    RsuVesting,

    /// <summary>ESPP (Employee Stock Purchase Plan) discount — the discount portion is §6 employment income.</summary>
    EsppDiscount,

    /// <summary>Sale of shares — taxed under §10 (other income). Exempt if held &gt; 3 years.</summary>
    ShareSale,

    /// <summary>Dividend payment — taxed in the USA (W-8BEN), reported under §8 capital income.</summary>
    Dividend,

    /// <summary>Tax withheld abroad on dividends or other income.</summary>
    TaxWithheld
}
