using TaxAdvisorBot.Domain.Enums;
using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Domain.Tests;

public sealed class TaxReturnTests
{
    [Fact]
    public void NewTaxReturn_HasDraftStatus()
    {
        var taxReturn = new TaxReturn();

        Assert.Equal(FilingStatus.Draft, taxReturn.Status);
    }

    [Fact]
    public void NewTaxReturn_HasGeneratedId()
    {
        var taxReturn = new TaxReturn();

        Assert.False(string.IsNullOrWhiteSpace(taxReturn.Id));
    }

    [Fact]
    public void TwoNewTaxReturns_HaveDifferentIds()
    {
        var a = new TaxReturn();
        var b = new TaxReturn();

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void TotalGrossIncome_SumsAllSections()
    {
        var taxReturn = new TaxReturn
        {
            Section6GrossIncome = 100_000m,
            Section7GrossIncome = 50_000m,
            Section8Income = 20_000m,
            Section9Income = 30_000m,
            Section10Income = 10_000m,
        };

        Assert.Equal(210_000m, taxReturn.TotalGrossIncome);
    }

    [Fact]
    public void TotalExpenses_SumsNonEmploymentSections()
    {
        var taxReturn = new TaxReturn
        {
            Section7Expenses = 25_000m,
            Section8Expenses = 5_000m,
            Section9Expenses = 10_000m,
            Section10Expenses = 3_000m,
        };

        Assert.Equal(43_000m, taxReturn.TotalExpenses);
    }

    [Fact]
    public void TaxableBase_IsGrossMinusExpenses()
    {
        var taxReturn = new TaxReturn
        {
            Section6GrossIncome = 100_000m,
            Section7GrossIncome = 50_000m,
            Section7Expenses = 25_000m,
            Section10Income = 10_000m,
            Section10Expenses = 3_000m,
        };

        // TotalGross = 100k + 50k + 10k = 160k
        // TotalExpenses = 25k + 3k = 28k
        Assert.Equal(132_000m, taxReturn.TaxableBase);
    }

    [Fact]
    public void TaxableBase_WithZeroIncome_IsZero()
    {
        var taxReturn = new TaxReturn();

        Assert.Equal(0m, taxReturn.TaxableBase);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100_000, 30_000, 70_000)]
    [InlineData(500_000, 500_000, 0)]
    public void TaxableBase_VariousScenarios(
        decimal section7Income, decimal section7Expenses, decimal expectedBase)
    {
        var taxReturn = new TaxReturn
        {
            Section7GrossIncome = section7Income,
            Section7Expenses = section7Expenses,
        };

        Assert.Equal(expectedBase, taxReturn.TaxableBase);
    }

    [Fact]
    public void TotalNonTaxableDeductions_SumsAllDeductions()
    {
        var taxReturn = new TaxReturn
        {
            PensionFundContributions = 24_000m,
            LifeInsuranceContributions = 24_000m,
            MortgageInterestPaid = 100_000m,
            CharitableDonations = 5_000m,
            TradeUnionFees = 1_000m,
        };

        Assert.Equal(154_000m, taxReturn.TotalNonTaxableDeductions);
    }

    [Fact]
    public void TotalTaxCredits_SumsAllCredits()
    {
        var taxReturn = new TaxReturn
        {
            BasicTaxCredit = 30_840m,
            SpouseTaxCredit = 24_840m,
            StudentTaxCredit = 4_020m,
        };

        Assert.Equal(59_700m, taxReturn.TotalTaxCredits);
    }

    [Fact]
    public void StockTransactions_DefaultsToEmptyList()
    {
        var taxReturn = new TaxReturn();

        Assert.Empty(taxReturn.StockTransactions);
    }
}
