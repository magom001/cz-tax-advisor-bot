using TaxAdvisorBot.Domain.Enums;
using TaxAdvisorBot.Domain.Models;
using TaxAdvisorBot.Infrastructure.AI.Plugins;

namespace TaxAdvisorBot.Infrastructure.Tests;

public sealed class TaxValidationPluginTests
{
    private readonly TaxValidationPlugin _plugin = new();

    [Fact]
    public void CompleteTaxReturn_ReturnsNoMissingFields()
    {
        var taxReturn = CreateCompleteTaxReturn();

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Empty(missing);
    }

    [Fact]
    public void MissingFirstName_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.FirstName = null;

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("First name"));
    }

    [Fact]
    public void MissingLastName_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.LastName = null;

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("Last name"));
    }

    [Fact]
    public void MissingDateOfBirth_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.DateOfBirth = null;

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("Date of birth"));
    }

    [Fact]
    public void MissingPersonalIdNumber_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.PersonalIdNumber = null;

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("Personal ID"));
    }

    [Fact]
    public void ZeroTaxYear_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.TaxYear = 0;

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("Tax year"));
    }

    [Fact]
    public void EmploymentIncome_WithoutTaxWithheld_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.Section6GrossIncome = 600_000m;
        taxReturn.Section6TaxWithheld = 0m;

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("tax withheld"));
    }

    [Fact]
    public void ShareSale_WithoutSaleDate_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.StockTransactions.Add(new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "MSFT",
            Quantity = 10,
            AcquisitionDate = new DateOnly(2023, 1, 1),
            SaleDate = null,
            AcquisitionPricePerShare = 300m,
            SalePricePerShare = 400m,
            CurrencyCode = "USD",
            ExchangeRate = 23.5m,
        });

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("MSFT") && m.Contains("sale date"));
    }

    [Fact]
    public void ShareSale_WithoutSalePrice_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.StockTransactions.Add(new StockTransaction
        {
            TransactionType = StockTransactionType.ShareSale,
            Ticker = "GOOG",
            Quantity = 5,
            AcquisitionDate = new DateOnly(2023, 1, 1),
            SaleDate = new DateOnly(2024, 6, 1),
            AcquisitionPricePerShare = 100m,
            SalePricePerShare = null,
            CurrencyCode = "USD",
            ExchangeRate = 23.5m,
        });

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("GOOG") && m.Contains("sale price"));
    }

    [Fact]
    public void Transaction_WithoutExchangeRate_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.StockTransactions.Add(new StockTransaction
        {
            TransactionType = StockTransactionType.RsuVesting,
            Ticker = "MSFT",
            Quantity = 20,
            AcquisitionDate = new DateOnly(2024, 3, 15),
            AcquisitionPricePerShare = 400m,
            CurrencyCode = "USD",
            ExchangeRate = null,
        });

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("MSFT") && m.Contains("exchange rate"));
    }

    [Fact]
    public void ForeignIncome_WithoutTaxPaid_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.HasForeignIncome = true;
        taxReturn.TaxPaidAbroad = 0m;
        taxReturn.ForeignIncomeCurrency = "USD";

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("tax paid abroad"));
    }

    [Fact]
    public void ForeignIncome_WithoutCurrency_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.HasForeignIncome = true;
        taxReturn.TaxPaidAbroad = 5_000m;
        taxReturn.ForeignIncomeCurrency = null;

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("currency"));
    }

    [Fact]
    public void DependentChildren_WithZeroBenefit_IsReported()
    {
        var taxReturn = CreateCompleteTaxReturn();
        taxReturn.DependentChildrenCount = 2;
        taxReturn.ChildTaxBenefit = 0m;

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.Contains(missing, m => m.Contains("child tax benefit"));
    }

    [Fact]
    public void EmptyTaxReturn_ReportsMultipleMissingFields()
    {
        var taxReturn = new TaxReturn();

        var missing = _plugin.GetMissingFields(taxReturn);

        Assert.True(missing.Count >= 4); // At least name, DOB, personal ID, tax year
    }

    private static TaxReturn CreateCompleteTaxReturn()
    {
        return new TaxReturn
        {
            TaxYear = 2024,
            FirstName = "Jan",
            LastName = "Novák",
            DateOfBirth = new DateOnly(1985, 3, 15),
            PersonalIdNumber = "8503151234",
            Section6GrossIncome = 600_000m,
            Section6TaxWithheld = 90_000m,
        };
    }
}
