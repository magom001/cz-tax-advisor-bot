using System.ComponentModel;
using Microsoft.SemanticKernel;
using TaxAdvisorBot.Domain.Enums;
using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Infrastructure.AI.Plugins;

/// <summary>
/// Deterministic C# plugin that checks a TaxReturn for missing or invalid fields.
/// Used by the orchestrator to decide what questions to ask the user next.
/// </summary>
public sealed class TaxValidationPlugin
{
    [KernelFunction, Description("Checks a tax return for missing or incomplete fields. Returns a list of missing field descriptions, or empty if complete.")]
    public IReadOnlyList<string> GetMissingFields(TaxReturn taxReturn)
    {
        var missing = new List<string>();

        // Personal details
        if (string.IsNullOrWhiteSpace(taxReturn.FirstName))
            missing.Add("First name (jméno) is required.");
        if (string.IsNullOrWhiteSpace(taxReturn.LastName))
            missing.Add("Last name (příjmení) is required.");
        if (taxReturn.DateOfBirth is null)
            missing.Add("Date of birth (datum narození) is required.");
        if (string.IsNullOrWhiteSpace(taxReturn.PersonalIdNumber))
            missing.Add("Personal ID number (rodné číslo) is required.");
        if (taxReturn.TaxYear == 0)
            missing.Add("Tax year must be specified.");

        // Employment income validation
        if (taxReturn.Section6GrossIncome > 0 && taxReturn.Section6TaxWithheld == 0)
            missing.Add("Employment income declared but no tax withheld by employer (záloha na daň). Please provide your Potvrzení o zdanitelných příjmech.");

        // Stock transaction validation
        foreach (var tx in taxReturn.StockTransactions)
        {
            if (tx.TransactionType == StockTransactionType.ShareSale && !tx.SaleDate.HasValue)
                missing.Add($"Share sale for {tx.Ticker} is missing the sale date.");

            if (tx.TransactionType == StockTransactionType.ShareSale && !tx.SalePricePerShare.HasValue)
                missing.Add($"Share sale for {tx.Ticker} is missing the sale price per share.");

            if (tx.ExchangeRate is null or 0)
                missing.Add($"Transaction for {tx.Ticker} on {tx.AcquisitionDate} is missing the ČNB exchange rate.");
        }

        // Foreign income validation
        if (taxReturn.HasForeignIncome && taxReturn.TaxPaidAbroad == 0)
            missing.Add("Foreign income declared but no tax paid abroad specified. Provide W-8BEN or equivalent.");

        if (taxReturn.HasForeignIncome && string.IsNullOrWhiteSpace(taxReturn.ForeignIncomeCurrency))
            missing.Add("Foreign income declared but currency code is missing.");

        // Deduction validation — warn if claimed but zero
        if (taxReturn.DependentChildrenCount > 0 && taxReturn.ChildTaxBenefit == 0)
            missing.Add("Dependent children declared but child tax benefit amount is zero. Please calculate the benefit.");

        return missing;
    }
}
