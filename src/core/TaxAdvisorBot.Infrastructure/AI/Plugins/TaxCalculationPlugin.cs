using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace TaxAdvisorBot.Infrastructure.AI.Plugins;

/// <summary>
/// Deterministic C# tax calculation plugin. The LLM never does math — it calls these functions.
/// All amounts are in CZK.
/// </summary>
public sealed class TaxCalculationPlugin
{
    /// <summary>Czech flat income tax rate (15%).</summary>
    private const decimal BasicTaxRate = 0.15m;

    /// <summary>Solidarity surcharge rate (23%) on income exceeding the threshold.</summary>
    private const decimal SolidarityTaxRate = 0.23m;

    /// <summary>Annual threshold for solidarity surcharge (48× average wage, approx 1 935 552 CZK for 2024).</summary>
    private const decimal SolidarityThreshold = 1_935_552m;

    [KernelFunction, Description("Calculates tax for §6 employment income. Returns the tax base (super-gross concept abolished — tax base equals gross income).")]
    public decimal CalculateSection6TaxBase(
        [Description("Gross employment income in CZK")] decimal grossIncome,
        [Description("Social insurance paid by employer in CZK")] decimal socialInsurance,
        [Description("Health insurance paid by employer in CZK")] decimal healthInsurance)
    {
        // Since 2021 the super-gross concept is abolished.
        // Tax base for §6 = gross income (not gross + insurance).
        // Insurance paid by employer is no longer added to the tax base.
        return grossIncome;
    }

    [KernelFunction, Description("Calculates tax for §10 other income (share sales, crypto, occasional income). Income exempt under §4 should already be excluded.")]
    public decimal CalculateSection10Tax(
        [Description("Total §10 income in CZK (non-exempt only)")] decimal income,
        [Description("Total deductible expenses in CZK (acquisition costs)")] decimal expenses)
    {
        var taxableIncome = Math.Max(income - expenses, 0m);
        return Math.Round(taxableIncome * BasicTaxRate, 2);
    }

    [KernelFunction, Description("Calculates the full income tax liability from the tax base, applying the 15% base rate and 23% solidarity surcharge on income above the threshold.")]
    public decimal CalculateIncomeTax(
        [Description("Total annual tax base in CZK (sum of all §6–§10 partial bases minus §15 deductions)")] decimal taxBase)
    {
        if (taxBase <= 0)
            return 0m;

        decimal tax;
        if (taxBase <= SolidarityThreshold)
        {
            tax = taxBase * BasicTaxRate;
        }
        else
        {
            tax = SolidarityThreshold * BasicTaxRate
                + (taxBase - SolidarityThreshold) * SolidarityTaxRate;
        }

        // Tax is rounded down to whole CZK (§16 odst. 4)
        return Math.Floor(tax);
    }

    [KernelFunction, Description("Applies §15 non-taxable deductions (pension, life insurance, mortgage interest, donations) to the tax base. Returns the adjusted tax base.")]
    public decimal ApplyDeductions(
        [Description("Tax base before deductions in CZK")] decimal taxBase,
        [Description("Pension fund contributions in CZK (max 24 000 deductible)")] decimal pensionFund,
        [Description("Life insurance contributions in CZK (max 24 000 deductible)")] decimal lifeInsurance,
        [Description("Mortgage interest paid in CZK (max 150 000 deductible)")] decimal mortgageInterest,
        [Description("Charitable donations in CZK")] decimal donations)
    {
        var pensionDeduction = Math.Min(pensionFund, 24_000m);
        var lifeInsuranceDeduction = Math.Min(lifeInsurance, 24_000m);
        var mortgageDeduction = Math.Min(mortgageInterest, 150_000m);

        // Donations: min 2% of tax base or 1 000 CZK, max 15% of tax base
        var minDonation = Math.Max(taxBase * 0.02m, 1_000m);
        var maxDonation = taxBase * 0.15m;
        var donationDeduction = donations >= minDonation ? Math.Min(donations, maxDonation) : 0m;

        var totalDeductions = pensionDeduction + lifeInsuranceDeduction + mortgageDeduction + donationDeduction;

        return Math.Max(taxBase - totalDeductions, 0m);
    }

    [KernelFunction, Description("Applies §35ba tax credits (basic taxpayer, spouse, student) and §35c child tax benefit. Returns the final tax after credits.")]
    public decimal ApplyCredits(
        [Description("Computed tax amount before credits in CZK")] decimal tax,
        [Description("Basic taxpayer credit in CZK (30 840 for 2024)")] decimal basicCredit,
        [Description("Spouse credit in CZK (24 840 if applicable)")] decimal spouseCredit,
        [Description("Student credit in CZK (4 020 if applicable)")] decimal studentCredit,
        [Description("Total child tax benefit in CZK (daňové zvýhodnění)")] decimal childBenefit)
    {
        // §35ba credits reduce tax to minimum of 0
        var afterCredits = Math.Max(tax - basicCredit - spouseCredit - studentCredit, 0m);

        // §35c child benefit can create a tax bonus (negative tax = refund)
        return afterCredits - childBenefit;
    }
}
