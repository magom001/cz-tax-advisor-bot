using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using TaxAdvisorBot.Application.Interfaces;

namespace TaxAdvisorBot.Infrastructure.ExchangeRates;

/// <summary>
/// Fetches daily exchange rates from the Czech National Bank (ČNB) API.
/// Caches rates in Redis to avoid repeated HTTP calls.
/// </summary>
public sealed class CnbExchangeRateService : IExchangeRateService
{
    private const string BaseUrl = "https://api.cnb.cz/cnbapi/exrates/daily";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(30);

    private readonly HttpClient _httpClient;
    private readonly IDistributedCache _cache;
    private readonly IUniformRateRepository _uniformRates;
    private readonly ILogger<CnbExchangeRateService> _logger;

    public CnbExchangeRateService(
        HttpClient httpClient,
        IDistributedCache cache,
        IUniformRateRepository uniformRates,
        ILogger<CnbExchangeRateService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _uniformRates = uniformRates;
        _logger = logger;
    }

    public async Task<decimal> GetDailyRateAsync(DateOnly date, string currencyCode, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"cnb:rate:{date:yyyy-MM-dd}:{currencyCode.ToUpperInvariant()}";

        // Check Redis cache
        var cached = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for {Currency} on {Date}", currencyCode, date);
            return decimal.Parse(cached);
        }

        _logger.LogInformation("Fetching ČNB exchange rate for {Currency} on {Date}", currencyCode, date);

        var url = $"{BaseUrl}?date={date:yyyy-MM-dd}";
        var response = await _httpClient.GetFromJsonAsync(url, CnbJsonContext.Default.CnbExRateResponse, cancellationToken)
            ?? throw new InvalidOperationException($"Failed to get exchange rates from ČNB for {date}");

        var rateEntry = response.Rates?.FirstOrDefault(r =>
            string.Equals(r.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Currency {currencyCode} not found in ČNB rates for {date}");

        // ČNB returns rate per 'amount' units (e.g., 100 JPY = X CZK), normalize to 1 unit
        var rate = rateEntry.Rate / rateEntry.Amount;

        // Cache in Redis
        await _cache.SetStringAsync(cacheKey, rate.ToString(), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheDuration
        }, cancellationToken);

        _logger.LogDebug("Cached rate for {Currency} on {Date}: {Rate}", currencyCode, date, rate);
        return rate;
    }

    public async Task<decimal> GetUniformRateAsync(int year, string currencyCode, CancellationToken cancellationToken = default)
    {
        var rate = await _uniformRates.GetRateAsync(year, currencyCode, cancellationToken);
        return rate ?? throw new InvalidOperationException(
            $"No §38 uniform rate configured for {currencyCode} in {year}. Add it via the API or configuration.");
    }

    public async Task<decimal> ConvertToCzkAsync(DateOnly date, decimal amount, string currencyCode, CancellationToken cancellationToken = default)
    {
        var rate = await GetDailyRateAsync(date, currencyCode, cancellationToken);
        return Math.Round(amount * rate, 2);
    }
}

internal sealed class CnbExRateResponse
{
    [JsonPropertyName("rates")]
    public List<CnbRate>? Rates { get; set; }
}

internal sealed class CnbRate
{
    [JsonPropertyName("currencyCode")]
    public string CurrencyCode { get; set; } = "";

    [JsonPropertyName("rate")]
    public decimal Rate { get; set; }

    [JsonPropertyName("amount")]
    public int Amount { get; set; }
}

[JsonSerializable(typeof(CnbExRateResponse))]
internal sealed partial class CnbJsonContext : JsonSerializerContext;
