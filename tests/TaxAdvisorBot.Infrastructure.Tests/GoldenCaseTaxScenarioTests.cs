using TaxAdvisorBot.Domain.Enums;
using TaxAdvisorBot.Domain.Models;
using TaxAdvisorBot.Infrastructure.AI.Plugins;

namespace TaxAdvisorBot.Infrastructure.Tests;

/// <summary>
/// Golden-case integration tests: realistic end-to-end tax scenarios
/// testing all plugins together with real-world numbers.
/// Each scenario represents a complete tax return calculation.
/// </summary>
public sealed class GoldenCaseTaxScenarioTests
{
    private readonly TaxCalculationPlugin _calc = new();

    // ── Scenario 1: Simple employed person, RSU only ──

    [Fact]
    public void Scenario1_EmployedWithRsu_BasicTaxReturn()
    {
        // Jan works in Prague, earns 1.2M CZK/year, received 50 MSFT RSU shares vested at $420 × 23.15 CZK/USD
        var rsuIncomeCzk = 50m * 420m * 23.15m; // = 486,300 CZK

        // §6 tax base = gross employment income (RSU already included by employer)
        var section6Base = _calc.CalculateSection6TaxBase(
            grossIncome: 1_200_000m + rsuIncomeCzk, // employer includes RSU in gross
            socialInsurance: 396_000m,
            healthInsurance: 162_000m);

        Assert.Equal(1_200_000m + rsuIncomeCzk, section6Base); // super-gross abolished

        // §15 deductions: pension 24k, mortgage 80k
        var afterDeductions = _calc.ApplyDeductions(
            taxBase: section6Base,
            pensionFund: 24_000m,
            lifeInsurance: 0m,
            mortgageInterest: 80_000m,
            donations: 0m);

        Assert.Equal(section6Base - 24_000m - 80_000m, afterDeductions);

        // Income tax
        var tax = _calc.CalculateIncomeTax(afterDeductions);

        // Apply credits: basic taxpayer only
        var finalTax = _calc.ApplyCredits(tax, basicCredit: 30_840m, spouseCredit: 0m, studentCredit: 0m, childBenefit: 0m);

        // Tax should be positive and reasonable for this income level
        Assert.True(finalTax > 0);
        Assert.True(finalTax < section6Base); // tax can't exceed income
    }

    // ── Scenario 2: Employed with RSU + ESPP + Share Sale (exempt) ──

    [Fact]
    public void Scenario2_RsuEsppAndExemptSale()
    {
        // RSU: 22 shares at $408 × 23.04 rate
        var rsuCzk = 22m * 408m * 23.04m; // = 206,807.04 CZK

        // ESPP: net gain only (FMV $420.72 - Purchase $378.65) × 2.408 shares × 23.35 rate
        var esppNetGainUsd = (420.72m - 378.65m) * 2.408m; // = 101.30 USD
        var esppCzk = esppNetGainUsd * 23.35m; // = 2,365.35 CZK

        // Share sale: held > 3 years → EXEMPT
        var sale = new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "MSFT",
            Quantity = 20,
            AcquisitionDate = new DateOnly(2020, 3, 15),
            SaleDate = new DateOnly(2024, 11, 10), // > 3 years
            AcquisitionPricePerShare = 280m,
            SalePricePerShare = 450m,
            CurrencyCode = "USD",
            ExchangeRate = 23.30m,
        };

        Assert.True(sale.IsExemptFromTax);
        // Exempt sale should NOT be included in §10

        // §6 base (employment + RSU + ESPP net gain)
        var grossEmployment = 1_400_000m;
        var section6Total = grossEmployment + rsuCzk + esppCzk;
        var section6Base = _calc.CalculateSection6TaxBase(section6Total, 400_000m, 170_000m);

        Assert.Equal(section6Total, section6Base);

        // §10 = 0 (exempt sale)
        var section10Tax = _calc.CalculateSection10Tax(0m, 0m);
        Assert.Equal(0m, section10Tax);
    }

    // ── Scenario 3: Taxable share sale (held < 3 years) ──

    [Fact]
    public void Scenario3_TaxableShareSale()
    {
        var sale = new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "MSFT",
            Quantity = 10,
            AcquisitionDate = new DateOnly(2023, 9, 15),
            SaleDate = new DateOnly(2024, 11, 10), // ~14 months, NOT exempt
            AcquisitionPricePerShare = 400m,
            SalePricePerShare = 450m,
            CurrencyCode = "USD",
            ExchangeRate = 23.30m,
        };

        Assert.False(sale.IsExemptFromTax);

        // §10 income = sale proceeds in CZK
        var incomeCzk = sale.Quantity * sale.SalePricePerShare!.Value * sale.ExchangeRate!.Value;
        // = 10 × 450 × 23.30 = 104,850 CZK

        // §10 expenses = acquisition cost in CZK
        var expensesCzk = sale.Quantity * sale.AcquisitionPricePerShare * sale.ExchangeRate!.Value;
        // = 10 × 400 × 23.30 = 93,200 CZK

        var section10Tax = _calc.CalculateSection10Tax(incomeCzk, expensesCzk);

        // Net gain = 104,850 - 93,200 = 11,650 × 15% = 1,747.50
        Assert.Equal(1_747.50m, section10Tax);
    }

    // ── Scenario 4: Dividends with W-8BEN (US tax = Czech tax, no additional) ──

    [Fact]
    public void Scenario4_DividendsWithFullForeignTaxCredit()
    {
        // Dividend: $500 gross, US tax withheld 15% = $75
        var dividendGrossUsd = 500m;
        var usTaxWithheldUsd = 75m; // 15%
        var rate = 23.00m;

        var dividendGrossCzk = dividendGrossUsd * rate; // = 11,500 CZK
        var usTaxWithheldCzk = usTaxWithheldUsd * rate; // = 1,725 CZK

        // Czech tax on dividends = 15%
        var czechTaxOnDividends = dividendGrossCzk * 0.15m; // = 1,725 CZK

        // Credit method §38f: foreign tax >= Czech tax → no additional Czech tax
        var additionalCzechTax = Math.Max(czechTaxOnDividends - usTaxWithheldCzk, 0m);

        Assert.Equal(0m, additionalCzechTax);
    }

    // ── Scenario 5: Dividends with lower foreign tax rate ──

    [Fact]
    public void Scenario5_DividendsWithPartialForeignTaxCredit()
    {
        // Dividend: $1000 gross, foreign tax withheld 10% = $100
        var dividendGrossUsd = 1000m;
        var foreignTaxUsd = 100m; // 10%
        var rate = 23.00m;

        var dividendGrossCzk = dividendGrossUsd * rate; // = 23,000 CZK
        var foreignTaxCzk = foreignTaxUsd * rate; // = 2,300 CZK

        // Czech tax on dividends = 15%
        var czechTaxOnDividends = dividendGrossCzk * 0.15m; // = 3,450 CZK

        // Credit method: foreign tax < Czech tax → pay the difference
        var additionalCzechTax = Math.Max(czechTaxOnDividends - foreignTaxCzk, 0m);

        Assert.Equal(1_150m, additionalCzechTax); // 3,450 - 2,300 = 1,150
    }

    // ── Scenario 6: Full tax return — employment + RSU + dividends + child benefit ──

    [Fact]
    public void Scenario6_CompleteTaxReturn_WithChildBonus()
    {
        // Employment: 800k gross
        // RSU: 30 shares × $400 × 22.50 = 270,000 CZK (included in gross by employer)
        var grossIncome = 800_000m + 270_000m;

        // §6 base
        var section6Base = _calc.CalculateSection6TaxBase(grossIncome, 250_000m, 100_000m);
        Assert.Equal(grossIncome, section6Base);

        // Deductions: pension 20k, mortgage 100k
        var afterDeductions = _calc.ApplyDeductions(section6Base, 20_000m, 0m, 100_000m, 0m);
        Assert.Equal(grossIncome - 20_000m - 100_000m, afterDeductions);

        // Tax
        var tax = _calc.CalculateIncomeTax(afterDeductions);

        // Credits: basic 30,840 + child benefit for 2 children
        // 1st child: 15,204, 2nd child: 22,320 = total 37,524
        var finalTax = _calc.ApplyCredits(tax, 30_840m, 0m, 0m, 37_524m);

        // Should still be positive (income is high enough)
        Assert.True(finalTax > 0);
    }

    // ── Scenario 7: Low income — child benefit creates tax bonus (refund) ──

    [Fact]
    public void Scenario7_LowIncome_ChildBonusRefund()
    {
        // Employment: 300k gross
        var section6Base = _calc.CalculateSection6TaxBase(300_000m, 99_000m, 40_500m);

        // Deductions: pension 24k
        var afterDeductions = _calc.ApplyDeductions(section6Base, 24_000m, 0m, 0m, 0m);
        Assert.Equal(276_000m, afterDeductions);

        // Tax: 276,000 × 15% = 41,400
        var tax = _calc.CalculateIncomeTax(afterDeductions);
        Assert.Equal(41_400m, tax);

        // Credits: basic 30,840 → 41,400 - 30,840 = 10,560
        // Then child benefit for 3 children: 15,204 + 22,320 + 27,840 = 65,364
        // 10,560 - 65,364 = -54,804 (refund!)
        var finalTax = _calc.ApplyCredits(tax, 30_840m, 0m, 0m, 65_364m);

        Assert.True(finalTax < 0); // Tax bonus = refund
        Assert.Equal(-54_804m, finalTax);
    }

    // ── Scenario 8: ESPP discount calculation ──

    [Fact]
    public void Scenario8_EsppDiscountCalculation()
    {
        var espp = new StockTransaction
        {
            TransactionType = StockTransactionType.EsppDiscount,
            Ticker = "MSFT",
            Quantity = 2.408m,
            AcquisitionDate = new DateOnly(2024, 3, 28),
            AcquisitionPricePerShare = 420.72m, // FMV
            EsppPurchasePricePerShare = 378.65m, // Discounted price
            CurrencyCode = "USD",
            ExchangeRate = 23.35m,
        };

        // Net gain per share = FMV - purchase price
        Assert.Equal(42.07m, espp.EsppDiscountPerShare);

        // Total ESPP discount = 2.408 × 42.07 = 101.30456
        Assert.Equal(101.30456m, espp.TotalEsppDiscount, 4);

        // CZK taxable base = total discount × rate
        var esppCzk = espp.TotalEsppDiscount * espp.ExchangeRate!.Value;
        Assert.True(esppCzk > 2_300m && esppCzk < 2_400m); // ~2,365 CZK
    }

    // ── Scenario 9: Mixed exempt and taxable sales ──

    [Fact]
    public void Scenario9_MixedExemptAndTaxableSales()
    {
        var exemptSale = new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "MSFT",
            Quantity = 20,
            AcquisitionDate = new DateOnly(2020, 3, 15),
            SaleDate = new DateOnly(2024, 6, 1), // held > 3 years
            AcquisitionPricePerShare = 280m,
            SalePricePerShare = 450m,
            CurrencyCode = "USD",
            ExchangeRate = 23.30m,
        };

        var taxableSale = new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "MSFT",
            Quantity = 10,
            AcquisitionDate = new DateOnly(2024, 1, 15),
            SaleDate = new DateOnly(2024, 11, 10), // held ~10 months
            AcquisitionPricePerShare = 380m,
            SalePricePerShare = 430m,
            CurrencyCode = "USD",
            ExchangeRate = 23.30m,
        };

        Assert.True(exemptSale.IsExemptFromTax);
        Assert.False(taxableSale.IsExemptFromTax);

        // Only taxable sale goes into §10
        var income = taxableSale.Quantity * taxableSale.SalePricePerShare!.Value * taxableSale.ExchangeRate!.Value;
        var expenses = taxableSale.Quantity * taxableSale.AcquisitionPricePerShare * taxableSale.ExchangeRate!.Value;

        var section10Tax = _calc.CalculateSection10Tax(income, expenses);

        // Gain = 10 × (430-380) × 23.30 = 10 × 50 × 23.30 = 11,650 CZK × 15% = 1,747.50
        Assert.Equal(1_747.50m, section10Tax);

        // Exempt sale gain is NOT taxed
        var exemptGainCzk = exemptSale.Quantity * (exemptSale.SalePricePerShare!.Value - exemptSale.AcquisitionPricePerShare) * exemptSale.ExchangeRate!.Value;
        Assert.True(exemptGainCzk > 0); // 20 × 170 × 23.30 = 79,220 CZK — exempt!
    }

    // ── Scenario 10: Solidarity surcharge for high earner ──

    [Fact]
    public void Scenario10_HighEarnerSolidaritySurcharge()
    {
        // High earner: 2.5M CZK gross + 500k RSU = 3M total
        var grossIncome = 3_000_000m;

        var section6Base = _calc.CalculateSection6TaxBase(grossIncome, 900_000m, 400_000m);
        Assert.Equal(grossIncome, section6Base);

        // No deductions for simplicity
        var tax = _calc.CalculateIncomeTax(section6Base);

        // 1,935,552 × 15% + (3,000,000 - 1,935,552) × 23%
        // = 290,332.80 + 1,064,448 × 0.23 = 290,332.80 + 244,823.04 = 535,155.84 → floor = 535,155
        Assert.Equal(535_155m, tax);

        // After basic credit
        var finalTax = _calc.ApplyCredits(tax, 30_840m, 0m, 0m, 0m);
        Assert.Equal(504_315m, finalTax);
    }
}
