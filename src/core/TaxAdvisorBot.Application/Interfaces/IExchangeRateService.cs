namespace TaxAdvisorBot.Application.Interfaces;

/// <summary>
/// Fetches exchange rates from the Czech National Bank (ČNB).
/// Supports both daily rates and §38 uniform yearly rates.
/// </summary>
public interface IExchangeRateService
{
    /// <summary>
    /// Gets the ČNB daily exchange rate for a currency on a given date (CZK per 1 unit).
    /// </summary>
    Task<decimal> GetDailyRateAsync(DateOnly date, string currencyCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the §38 uniform yearly exchange rate (CZK per 1 unit). Manually configured.
    /// </summary>
    Task<decimal> GetUniformRateAsync(int year, string currencyCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an amount to CZK using the daily rate for the given date.
    /// </summary>
    Task<decimal> ConvertToCzkAsync(DateOnly date, decimal amount, string currencyCode, CancellationToken cancellationToken = default);
}
