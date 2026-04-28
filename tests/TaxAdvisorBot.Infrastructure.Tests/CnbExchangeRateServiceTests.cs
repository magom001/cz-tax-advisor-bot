using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Infrastructure.ExchangeRates;

namespace TaxAdvisorBot.Infrastructure.Tests;

public sealed class CnbExchangeRateServiceTests
{
    private const string SampleCnbResponse = """
    {
        "rates": [
            { "currencyCode": "USD", "rate": 23.145, "amount": 1 },
            { "currencyCode": "EUR", "rate": 25.320, "amount": 1 },
            { "currencyCode": "GBP", "rate": 29.456, "amount": 1 },
            { "currencyCode": "JPY", "rate": 15.234, "amount": 100 }
        ]
    }
    """;

    private static CnbExchangeRateService CreateService(IDistributedCache? cache = null)
    {
        var handler = new FakeHttpHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(SampleCnbResponse, Encoding.UTF8, "application/json")
        });
        var httpClient = new HttpClient(handler);
        var distributedCache = cache ?? new FakeDistributedCache();
        var uniformRates = new FakeUniformRateRepository();

        return new CnbExchangeRateService(
            httpClient,
            distributedCache,
            uniformRates,
            NullLogger<CnbExchangeRateService>.Instance);
    }

    [Fact]
    public async Task GetDailyRate_Usd_ReturnsCorrectRate()
    {
        var service = CreateService();

        var rate = await service.GetDailyRateAsync(new DateOnly(2024, 6, 15), "USD");

        Assert.Equal(23.145m, rate);
    }

    [Fact]
    public async Task GetDailyRate_Eur_ReturnsCorrectRate()
    {
        var service = CreateService();

        var rate = await service.GetDailyRateAsync(new DateOnly(2024, 6, 15), "EUR");

        Assert.Equal(25.320m, rate);
    }

    [Fact]
    public async Task GetDailyRate_Jpy_NormalizesPerAmount()
    {
        // JPY rate is 15.234 per 100 units → 0.15234 per 1 unit
        var service = CreateService();

        var rate = await service.GetDailyRateAsync(new DateOnly(2024, 6, 15), "JPY");

        Assert.Equal(0.15234m, rate);
    }

    [Fact]
    public async Task GetDailyRate_CaseInsensitive()
    {
        var service = CreateService();

        var rate = await service.GetDailyRateAsync(new DateOnly(2024, 6, 15), "usd");

        Assert.Equal(23.145m, rate);
    }

    [Fact]
    public async Task GetDailyRate_UnknownCurrency_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetDailyRateAsync(new DateOnly(2024, 6, 15), "XYZ"));
    }

    [Fact]
    public async Task ConvertToCzk_CalculatesCorrectly()
    {
        var service = CreateService();

        var czk = await service.ConvertToCzkAsync(new DateOnly(2024, 6, 15), 1000m, "USD");

        Assert.Equal(23_145.00m, czk); // 1000 × 23.145
    }

    [Fact]
    public async Task ConvertToCzk_RoundsToTwoDecimals()
    {
        var service = CreateService();

        var czk = await service.ConvertToCzkAsync(new DateOnly(2024, 6, 15), 33.33m, "USD");

        Assert.Equal(771.42m, czk); // 33.33 × 23.145 = 771.4219 → 771.42
    }

    [Fact]
    public async Task GetDailyRate_CachesResult()
    {
        var cache = new FakeDistributedCache();
        var service = CreateService(cache: cache);

        // First call — hits HTTP
        await service.GetDailyRateAsync(new DateOnly(2024, 6, 15), "USD");

        // Verify the value is cached
        var cached = await cache.GetStringAsync("cnb:rate:2024-06-15:USD");
        Assert.NotNull(cached);
        Assert.Equal("23.145", cached);
    }

    [Fact]
    public async Task GetUniformRate_Configured_ReturnsRate()
    {
        var repo = new FakeUniformRateRepository();
        await repo.SetRateAsync(2024, "USD", 23.14m);

        var service = new CnbExchangeRateService(
            new HttpClient(), new FakeDistributedCache(), repo,
            NullLogger<CnbExchangeRateService>.Instance);

        var rate = await service.GetUniformRateAsync(2024, "USD");

        Assert.Equal(23.14m, rate);
    }

    [Fact]
    public async Task GetUniformRate_NotConfigured_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetUniformRateAsync(2024, "USD"));
    }
}

/// <summary>Fake HTTP handler that returns a fixed response.</summary>
internal sealed class FakeHttpHandler(HttpResponseMessage response) : HttpMessageHandler
{
    public int CallCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(response);
    }
}

/// <summary>Simple in-memory distributed cache for testing.</summary>
internal sealed class FakeDistributedCache : IDistributedCache
{
    private readonly Dictionary<string, byte[]> _store = new();

    public byte[]? Get(string key) => _store.GetValueOrDefault(key);
    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult(Get(key));
    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => _store[key] = value;
    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        Set(key, value, options);
        return Task.CompletedTask;
    }
    public void Refresh(string key) { }
    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public Task RemoveAsync(string key, CancellationToken token = default) { Remove(key); return Task.CompletedTask; }
}

/// <summary>In-memory uniform rate repository for unit tests (no MongoDB).</summary>
internal sealed class FakeUniformRateRepository : IUniformRateRepository
{
    private readonly Dictionary<string, decimal> _rates = new(StringComparer.OrdinalIgnoreCase);

    public Task<decimal?> GetRateAsync(int year, string currencyCode, CancellationToken ct = default)
    {
        var key = $"{year}:{currencyCode.ToUpperInvariant()}";
        return Task.FromResult(_rates.TryGetValue(key, out var rate) ? (decimal?)rate : null);
    }

    public Task SetRateAsync(int year, string currencyCode, decimal rate, CancellationToken ct = default)
    {
        _rates[$"{year}:{currencyCode.ToUpperInvariant()}"] = rate;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UniformRateEntry>> GetAllAsync(CancellationToken ct = default)
    {
        var entries = _rates.Select(kv =>
        {
            var parts = kv.Key.Split(':');
            return new UniformRateEntry(int.Parse(parts[0]), parts[1], kv.Value);
        }).ToList();
        return Task.FromResult<IReadOnlyList<UniformRateEntry>>(entries);
    }
}
