using TaxAdvisorBot.Infrastructure.AI.Plugins;

namespace TaxAdvisorBot.Infrastructure.Tests;

public sealed class TaxCalculationPluginTests
{
    private readonly TaxCalculationPlugin _plugin = new();

    // ── §6 Employment tax base ──

    [Fact]
    public void CalculateSection6TaxBase_ReturnsGrossIncome()
    {
        // Since 2021, super-gross concept abolished — tax base = gross income
        var result = _plugin.CalculateSection6TaxBase(600_000m, 148_200m, 54_000m);

        Assert.Equal(600_000m, result);
    }

    // ── §10 Other income ──

    [Fact]
    public void CalculateSection10Tax_BasicCalculation()
    {
        // 100k income - 30k expenses = 70k × 15% = 10 500
        var result = _plugin.CalculateSection10Tax(100_000m, 30_000m);

        Assert.Equal(10_500m, result);
    }

    [Fact]
    public void CalculateSection10Tax_ExpensesExceedIncome_ReturnsZero()
    {
        var result = _plugin.CalculateSection10Tax(50_000m, 80_000m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateSection10Tax_ZeroIncome_ReturnsZero()
    {
        var result = _plugin.CalculateSection10Tax(0m, 0m);

        Assert.Equal(0m, result);
    }

    // ── Full income tax (15% + 23% solidarity) ──

    [Fact]
    public void CalculateIncomeTax_BelowThreshold_15Percent()
    {
        // 1 000 000 × 15% = 150 000
        var result = _plugin.CalculateIncomeTax(1_000_000m);

        Assert.Equal(150_000m, result);
    }

    [Fact]
    public void CalculateIncomeTax_AboveThreshold_SolidaritySurcharge()
    {
        // 2 000 000: first 1 935 552 at 15%, remaining 64 448 at 23%
        // = 290 332.80 + 14 823.04 = 305 155.84 → floor = 305 155
        var result = _plugin.CalculateIncomeTax(2_000_000m);

        Assert.Equal(305_155m, result);
    }

    [Fact]
    public void CalculateIncomeTax_ExactlyAtThreshold()
    {
        // 1 935 552 × 15% = 290 332.80 → floor = 290 332
        var result = _plugin.CalculateIncomeTax(1_935_552m);

        Assert.Equal(290_332m, result);
    }

    [Fact]
    public void CalculateIncomeTax_ZeroBase_ReturnsZero()
    {
        var result = _plugin.CalculateIncomeTax(0m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateIncomeTax_NegativeBase_ReturnsZero()
    {
        var result = _plugin.CalculateIncomeTax(-50_000m);

        Assert.Equal(0m, result);
    }

    // ── §15 Deductions ──

    [Fact]
    public void ApplyDeductions_CapsAtMaximums()
    {
        // All over max: pension 30k→24k, life 30k→24k, mortgage 200k→150k
        // Donations 20k: min = max(1M×0.02, 1000)=20k, max = 1M×0.15=150k → 20k
        // Total deductions = 24k + 24k + 150k + 20k = 218k
        // 1M - 218k = 782k
        var result = _plugin.ApplyDeductions(1_000_000m, 30_000m, 30_000m, 200_000m, 20_000m);

        Assert.Equal(782_000m, result);
    }

    [Fact]
    public void ApplyDeductions_DonationBelowMinimum_Ignored()
    {
        // Tax base 500k, donation 500 CZK — min is max(500k×0.02, 1000) = 10 000 → 500 < 10k → ignored
        var result = _plugin.ApplyDeductions(500_000m, 0m, 0m, 0m, 500m);

        Assert.Equal(500_000m, result);
    }

    [Fact]
    public void ApplyDeductions_ZeroDeductions_ReturnsOriginalBase()
    {
        var result = _plugin.ApplyDeductions(800_000m, 0m, 0m, 0m, 0m);

        Assert.Equal(800_000m, result);
    }

    [Fact]
    public void ApplyDeductions_ResultNeverNegative()
    {
        var result = _plugin.ApplyDeductions(10_000m, 24_000m, 24_000m, 150_000m, 0m);

        Assert.Equal(0m, result);
    }

    // ── §35ba/§35c Credits ──

    [Fact]
    public void ApplyCredits_BasicTaxpayerCredit()
    {
        // Tax 150 000 - basic 30 840 = 119 160
        var result = _plugin.ApplyCredits(150_000m, 30_840m, 0m, 0m, 0m);

        Assert.Equal(119_160m, result);
    }

    [Fact]
    public void ApplyCredits_AllCredits()
    {
        // Tax 150k - basic 30.84k - spouse 24.84k - student 4.02k = 90 300
        // 90 300 - child 15 204 = 75 096
        var result = _plugin.ApplyCredits(150_000m, 30_840m, 24_840m, 4_020m, 15_204m);

        Assert.Equal(75_096m, result);
    }

    [Fact]
    public void ApplyCredits_CreditsExceedTax_ButChildBenefitCreatesBonus()
    {
        // Tax 20k - basic 30.84k = 0 (capped) - child 15 204 = -15 204 (bonus/refund)
        var result = _plugin.ApplyCredits(20_000m, 30_840m, 0m, 0m, 15_204m);

        Assert.Equal(-15_204m, result);
    }

    [Fact]
    public void ApplyCredits_CreditsExceedTax_NoChild_ReturnsZero()
    {
        var result = _plugin.ApplyCredits(20_000m, 30_840m, 0m, 0m, 0m);

        Assert.Equal(0m, result);
    }
}
