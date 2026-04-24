using TaxAdvisorBot.Domain.Enums;

namespace TaxAdvisorBot.Domain.Models;

/// <summary>
/// Represents the structured state of a personal income tax filing (DPFO).
/// Focused on: employment income, stock compensation (RSU/ESPP/share sales), and standard Czech deductions.
/// Mutable — fields are populated incrementally as documents are processed and the user provides information.
/// </summary>
public sealed class TaxReturn
{
    /// <summary>Unique identifier for this tax return.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Tax year this return covers.</summary>
    public int TaxYear { get; set; }

    /// <summary>Current status of the filing.</summary>
    public FilingStatus Status { get; set; } = FilingStatus.Draft;

    // ── Personal details ──

    /// <summary>Taxpayer's first name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Taxpayer's last name.</summary>
    public string? LastName { get; set; }

    /// <summary>Taxpayer's date of birth.</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Czech personal identification number (rodné číslo). Sensitive — handle with care.</summary>
    public string? PersonalIdNumber { get; set; }

    /// <summary>Tax office code (finanční úřad).</summary>
    public string? TaxOfficeCode { get; set; }

    // ── §6 — Employment income ──

    /// <summary>§6 — Gross employment income (including RSU vesting value and ESPP discount).</summary>
    public decimal Section6GrossIncome { get; set; }

    /// <summary>§6 — Social insurance paid by employer (34% of super-gross).</summary>
    public decimal Section6SocialInsurance { get; set; }

    /// <summary>§6 — Health insurance paid by employer.</summary>
    public decimal Section6HealthInsurance { get; set; }

    /// <summary>§6 — Tax advance already withheld by employer.</summary>
    public decimal Section6TaxWithheld { get; set; }

    // ── §7 — Self-employment (optional, included for completeness) ──

    /// <summary>§7 — Self-employment gross income.</summary>
    public decimal Section7GrossIncome { get; set; }

    /// <summary>§7 — Deductible expenses (actual or flat-rate).</summary>
    public decimal Section7Expenses { get; set; }

    // ── §8 — Capital income (dividends, interest) ──

    /// <summary>§8 — Capital income (dividends, interest).</summary>
    public decimal Section8Income { get; set; }

    /// <summary>§8 — Deductible costs related to capital income.</summary>
    public decimal Section8Expenses { get; set; }

    // ── §9 — Rental income ──

    /// <summary>§9 — Rental income.</summary>
    public decimal Section9Income { get; set; }

    /// <summary>§9 — Deductible rental expenses.</summary>
    public decimal Section9Expenses { get; set; }

    // ── §10 — Other income (share sales, crypto, occasional income) ──

    /// <summary>§10 — Other income (non-exempt share sales, crypto, occasional sales).</summary>
    public decimal Section10Income { get; set; }

    /// <summary>§10 — Deductible expenses for other income (acquisition cost of sold shares, etc.).</summary>
    public decimal Section10Expenses { get; set; }

    // ── Stock transactions ──

    /// <summary>Individual stock transactions (RSU vests, ESPP purchases, share sales).</summary>
    public List<StockTransaction> StockTransactions { get; set; } = [];

    // ── Foreign income ──

    /// <summary>Whether the taxpayer has foreign-sourced income.</summary>
    public bool HasForeignIncome { get; set; }

    /// <summary>Total tax already paid abroad (for credit method under §38f).</summary>
    public decimal TaxPaidAbroad { get; set; }

    /// <summary>ISO 4217 currency code of the foreign income (e.g. "USD").</summary>
    public string? ForeignIncomeCurrency { get; set; }

    // ── §15 — Nezdanitelné části základu daně (non-taxable deductions) ──

    /// <summary>§15 odst. 3 — Pension fund contributions (penzijní připojištění/spoření). Max 24 000 CZK deductible.</summary>
    public decimal PensionFundContributions { get; set; }

    /// <summary>§15 odst. 4 — Life insurance contributions (životní pojištění). Max 24 000 CZK deductible.</summary>
    public decimal LifeInsuranceContributions { get; set; }

    /// <summary>§15 odst. 3 — Mortgage interest paid (úroky z úvěru na bydlení). Max 150 000 CZK deductible.</summary>
    public decimal MortgageInterestPaid { get; set; }

    /// <summary>§15 odst. 1 — Charitable donations (dary). 2–15% of tax base.</summary>
    public decimal CharitableDonations { get; set; }

    /// <summary>§15 odst. 5 — Trade union membership fees.</summary>
    public decimal TradeUnionFees { get; set; }

    // ── §35ba — Slevy na dani (tax credits) ──

    /// <summary>§35ba odst. 1 písm. a — Basic taxpayer credit (sleva na poplatníka), currently 30 840 CZK.</summary>
    public decimal BasicTaxCredit { get; set; }

    /// <summary>§35ba odst. 1 písm. b — Spouse credit (sleva na manžela/manželku), 24 840 CZK.</summary>
    public decimal SpouseTaxCredit { get; set; }

    /// <summary>§35ba odst. 1 písm. f — Student credit (sleva na studenta), 4 020 CZK.</summary>
    public decimal StudentTaxCredit { get; set; }

    // ── §35c — Daňové zvýhodnění na děti (child tax benefit) ──

    /// <summary>Number of dependent children for tax benefit calculation.</summary>
    public int DependentChildrenCount { get; set; }

    /// <summary>Total child tax benefit (daňové zvýhodnění). Amount depends on child count and order.</summary>
    public decimal ChildTaxBenefit { get; set; }

    // ── Output ──

    /// <summary>Path to the generated XML file for electronic submission to Financial Administration.</summary>
    public string? GeneratedXmlPath { get; set; }

    /// <summary>Path to the generated PDF form.</summary>
    public string? GeneratedPdfPath { get; set; }

    // ── Computed fields ──

    /// <summary>Total gross income across all sections.</summary>
    public decimal TotalGrossIncome =>
        Section6GrossIncome + Section7GrossIncome + Section8Income + Section9Income + Section10Income;

    /// <summary>Total deductible expenses across §7–§10.</summary>
    public decimal TotalExpenses =>
        Section7Expenses + Section8Expenses + Section9Expenses + Section10Expenses;

    /// <summary>Total §15 non-taxable deductions.</summary>
    public decimal TotalNonTaxableDeductions =>
        PensionFundContributions + LifeInsuranceContributions + MortgageInterestPaid
        + CharitableDonations + TradeUnionFees;

    /// <summary>Total §35ba tax credits.</summary>
    public decimal TotalTaxCredits =>
        BasicTaxCredit + SpouseTaxCredit + StudentTaxCredit;

    /// <summary>Net taxable base before non-taxable deductions.</summary>
    public decimal TaxableBase => TotalGrossIncome - TotalExpenses;
}
