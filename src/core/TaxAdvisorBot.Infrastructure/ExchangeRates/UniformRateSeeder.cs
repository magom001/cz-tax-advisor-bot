using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaxAdvisorBot.Application.Interfaces;

namespace TaxAdvisorBot.Infrastructure.ExchangeRates;

/// <summary>
/// Seeds uniform exchange rates from appsettings.json into MongoDB on startup.
/// Config format: "UniformRates": { "2024:USD": 23.14, "2025:USD": 23.48 }
/// </summary>
public sealed class UniformRateSeeder : IHostedService
{
    private readonly IConfiguration _config;
    private readonly IUniformRateRepository _repository;
    private readonly ILogger<UniformRateSeeder> _logger;

    public UniformRateSeeder(IConfiguration config, IUniformRateRepository repository, ILogger<UniformRateSeeder> logger)
    {
        _config = config;
        _repository = repository;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var section = _config.GetSection("UniformRates");
        if (!section.Exists()) return;

        foreach (var entry in section.GetChildren())
        {
            var key = entry.Key; // "2024:USD"
            var parts = key.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out var year))
            {
                _logger.LogWarning("Invalid uniform rate key '{Key}', expected format 'YYYY:CUR'", key);
                continue;
            }

            if (!decimal.TryParse(entry.Value, out var rate))
            {
                _logger.LogWarning("Invalid uniform rate value for '{Key}': '{Value}'", key, entry.Value);
                continue;
            }

            await _repository.SetRateAsync(year, parts[1], rate, cancellationToken);
            _logger.LogInformation("Seeded uniform rate: {Year}:{Currency} = {Rate}", year, parts[1], rate);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
