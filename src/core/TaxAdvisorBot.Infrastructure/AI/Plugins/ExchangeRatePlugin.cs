using System.ComponentModel;
using Microsoft.SemanticKernel;
using TaxAdvisorBot.Application.Interfaces;

namespace TaxAdvisorBot.Infrastructure.AI.Plugins;

/// <summary>
/// Semantic Kernel plugin for currency conversion using ČNB exchange rates.
/// </summary>
public sealed class ExchangeRatePlugin
{
    private readonly IExchangeRateService _exchangeRateService;

    public ExchangeRatePlugin(IExchangeRateService exchangeRateService)
    {
        _exchangeRateService = exchangeRateService;
    }

    [KernelFunction, Description("Converts a foreign currency amount to CZK using the official ČNB daily exchange rate for a given date.")]
    public async Task<decimal> ConvertToCzkAsync(
        [Description("Amount in foreign currency")] decimal amount,
        [Description("ISO 4217 currency code (e.g. USD, EUR)")] string currencyCode,
        [Description("Date for the exchange rate in yyyy-MM-dd format")] string date,
        CancellationToken cancellationToken = default)
    {
        var dateOnly = DateOnly.Parse(date);
        return await _exchangeRateService.ConvertToCzkAsync(dateOnly, amount, currencyCode, cancellationToken);
    }

    [KernelFunction, Description("Gets the ČNB daily exchange rate (CZK per 1 unit of foreign currency) for a given date.")]
    public async Task<decimal> GetDailyRateAsync(
        [Description("ISO 4217 currency code (e.g. USD, EUR)")] string currencyCode,
        [Description("Date for the exchange rate in yyyy-MM-dd format")] string date,
        CancellationToken cancellationToken = default)
    {
        var dateOnly = DateOnly.Parse(date);
        return await _exchangeRateService.GetDailyRateAsync(dateOnly, currencyCode, cancellationToken);
    }

    [KernelFunction, Description("Gets the §38 uniform yearly exchange rate (CZK per 1 unit) used for annual tax calculations.")]
    public async Task<decimal> GetUniformRateAsync(
        [Description("Tax year")] int year,
        [Description("ISO 4217 currency code (e.g. USD)")] string currencyCode,
        CancellationToken cancellationToken = default)
    {
        return await _exchangeRateService.GetUniformRateAsync(year, currencyCode, cancellationToken);
    }
}
