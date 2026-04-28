using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Infrastructure.AI.Plugins;

/// <summary>
/// Plugin that gives the agent access to the persisted TaxReturn data.
/// Allows reading extracted document data, stock transactions, and current filing state.
/// </summary>
public sealed class TaxReturnPlugin
{
    private readonly ITaxReturnRepository _repo;

    public TaxReturnPlugin(ITaxReturnRepository repo)
    {
        _repo = repo;
    }

    [KernelFunction, Description("Gets the current tax return data for a given year, including all stock transactions extracted from uploaded documents.")]
    public async Task<string> GetTaxReturnAsync(
        [Description("Tax year (e.g. 2025)")] int year,
        CancellationToken cancellationToken = default)
    {
        var taxReturn = await _repo.GetByYearAsync(year, cancellationToken);
        if (taxReturn is null)
            return $"No tax return found for year {year}. Ask the user to upload their documents first.";

        var summary = new
        {
            taxReturn.TaxYear,
            taxReturn.FirstName,
            taxReturn.LastName,
            Section6_GrossIncome = taxReturn.Section6GrossIncome,
            Section6_TaxWithheld = taxReturn.Section6TaxWithheld,
            Section10_Income = taxReturn.Section10Income,
            Section10_Expenses = taxReturn.Section10Expenses,
            TotalGrossIncome = taxReturn.TotalGrossIncome,
            TaxableBase = taxReturn.TaxableBase,
            StockTransactionCount = taxReturn.StockTransactions.Count,
            StockTransactions = taxReturn.StockTransactions.Select(t => new
            {
                Type = t.TransactionType.ToString(),
                t.Ticker,
                t.Quantity,
                t.AcquisitionDate,
                t.SaleDate,
                AcquisitionPrice = t.AcquisitionPricePerShare,
                SalePrice = t.SalePricePerShare,
                EsppDiscount = t.EsppPurchasePricePerShare,
                t.CurrencyCode,
                t.ExchangeRate,
                t.IsExemptFromTax,
                TotalCost = t.TotalAcquisitionCost,
                TotalProceeds = t.TotalSaleProceeds,
                CapitalGain = t.CapitalGain,
                EsppDiscountTotal = t.TotalEsppDiscount,
                t.GrossAmount,
                t.TaxWithheldAbroad,
            }),
            Deductions = new
            {
                taxReturn.PensionFundContributions,
                taxReturn.LifeInsuranceContributions,
                taxReturn.MortgageInterestPaid,
                taxReturn.CharitableDonations,
                Total = taxReturn.TotalNonTaxableDeductions,
            },
            Credits = new
            {
                taxReturn.BasicTaxCredit,
                taxReturn.SpouseTaxCredit,
                taxReturn.StudentTaxCredit,
                taxReturn.ChildTaxBenefit,
                taxReturn.DependentChildrenCount,
                Total = taxReturn.TotalTaxCredits,
            },
            HasForeignIncome = taxReturn.HasForeignIncome,
            taxReturn.TaxPaidAbroad,
        };

        return JsonSerializer.Serialize(summary, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    [KernelFunction, Description("Lists all tax returns available in the system.")]
    public async Task<string> ListTaxReturnsAsync(CancellationToken cancellationToken = default)
    {
        var returns = await _repo.GetAllAsync(cancellationToken);
        if (returns.Count == 0)
            return "No tax returns found. Ask the user to upload their documents.";

        var list = returns.Select(r => new
        {
            r.TaxYear,
            r.FirstName,
            r.LastName,
            r.Status,
            StockTransactions = r.StockTransactions.Count,
            GrossIncome = r.TotalGrossIncome,
        });

        return JsonSerializer.Serialize(list, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
