using TaxAdvisorBot.Domain.Enums;
using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Domain.Tests;

public sealed class StockTransactionTests
{
    // ── 3-year exemption rule (§4 odst. 1 písm. w) ──

    [Fact]
    public void ShareSale_HeldOver3Years_IsExempt()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "MSFT",
            Quantity = 10,
            AcquisitionDate = new DateOnly(2021, 3, 15),
            SaleDate = new DateOnly(2024, 6, 1), // > 3 years
            AcquisitionPricePerShare = 250m,
            SalePricePerShare = 400m,
            CurrencyCode = "USD",
        };

        Assert.True(tx.IsExemptFromTax);
    }

    [Fact]
    public void ShareSale_HeldExactly3Years_IsNotExempt()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "MSFT",
            Quantity = 10,
            AcquisitionDate = new DateOnly(2021, 3, 15),
            SaleDate = new DateOnly(2024, 3, 15), // exactly 3 years — not > 3 years
            AcquisitionPricePerShare = 250m,
            SalePricePerShare = 400m,
            CurrencyCode = "USD",
        };

        Assert.False(tx.IsExemptFromTax);
    }

    [Fact]
    public void ShareSale_HeldUnder3Years_IsNotExempt()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "GOOG",
            Quantity = 5,
            AcquisitionDate = new DateOnly(2023, 1, 10),
            SaleDate = new DateOnly(2024, 6, 1), // ~1.5 years
            AcquisitionPricePerShare = 100m,
            SalePricePerShare = 150m,
            CurrencyCode = "USD",
        };

        Assert.False(tx.IsExemptFromTax);
    }

    [Fact]
    public void RsuVesting_IsNeverExempt()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.RsuVesting,
            Ticker = "MSFT",
            Quantity = 20,
            AcquisitionDate = new DateOnly(2020, 1, 1),
            SaleDate = new DateOnly(2025, 6, 1),
            AcquisitionPricePerShare = 200m,
            SalePricePerShare = 400m,
            CurrencyCode = "USD",
        };

        // RSU vesting is §6 income, not §10 — exemption doesn't apply
        Assert.False(tx.IsExemptFromTax);
    }

    [Fact]
    public void ShareSale_WithNoSaleDate_IsNotExempt()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "MSFT",
            Quantity = 10,
            AcquisitionDate = new DateOnly(2020, 1, 1),
            SaleDate = null,
            AcquisitionPricePerShare = 200m,
            CurrencyCode = "USD",
        };

        Assert.False(tx.IsExemptFromTax);
    }

    // ── Capital gain calculation ──

    [Fact]
    public void CapitalGain_CalculatesCorrectly()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "MSFT",
            Quantity = 10,
            AcquisitionDate = new DateOnly(2023, 1, 1),
            SaleDate = new DateOnly(2024, 6, 1),
            AcquisitionPricePerShare = 250m,
            SalePricePerShare = 400m,
            CurrencyCode = "USD",
        };

        Assert.Equal(2_500m, tx.TotalAcquisitionCost); // 10 × 250
        Assert.Equal(4_000m, tx.TotalSaleProceeds);     // 10 × 400
        Assert.Equal(1_500m, tx.CapitalGain);            // 4000 - 2500
    }

    [Fact]
    public void CapitalGain_WithLoss_IsNegative()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "GOOG",
            Quantity = 5,
            AcquisitionDate = new DateOnly(2023, 1, 1),
            SaleDate = new DateOnly(2024, 1, 1),
            AcquisitionPricePerShare = 150m,
            SalePricePerShare = 100m,
            CurrencyCode = "USD",
        };

        Assert.Equal(-250m, tx.CapitalGain); // 500 - 750
    }

    [Fact]
    public void TotalSaleProceeds_WithNoSalePrice_IsZero()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.RsuVesting,
            Ticker = "MSFT",
            Quantity = 20,
            AcquisitionDate = new DateOnly(2024, 3, 15),
            AcquisitionPricePerShare = 400m,
            CurrencyCode = "USD",
        };

        Assert.Equal(0m, tx.TotalSaleProceeds);
    }

    // ── ESPP discount ──

    [Fact]
    public void EsppDiscount_Calculates10PercentDiscount()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.EsppDiscount,
            Ticker = "MSFT",
            Quantity = 50,
            AcquisitionDate = new DateOnly(2024, 6, 30),
            AcquisitionPricePerShare = 400m,       // FMV at purchase
            EsppPurchasePricePerShare = 360m,       // 10% discount
            CurrencyCode = "USD",
        };

        Assert.Equal(40m, tx.EsppDiscountPerShare);     // 400 - 360
        Assert.Equal(2_000m, tx.TotalEsppDiscount);      // 50 × 40
    }

    [Fact]
    public void EsppDiscount_WithNoEsppPrice_IsZero()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.RsuVesting,
            Ticker = "MSFT",
            Quantity = 10,
            AcquisitionDate = new DateOnly(2024, 3, 15),
            AcquisitionPricePerShare = 400m,
            CurrencyCode = "USD",
        };

        Assert.Equal(0m, tx.EsppDiscountPerShare);
        Assert.Equal(0m, tx.TotalEsppDiscount);
    }

    // ── Acquisition cost ──

    [Fact]
    public void TotalAcquisitionCost_CalculatesCorrectly()
    {
        var tx = new StockTransaction
        {
            TransactionType = StockTransactionType.RsuVesting,
            Ticker = "GOOG",
            Quantity = 100,
            AcquisitionDate = new DateOnly(2024, 1, 15),
            AcquisitionPricePerShare = 142.50m,
            CurrencyCode = "USD",
        };

        Assert.Equal(14_250m, tx.TotalAcquisitionCost);
    }
}
