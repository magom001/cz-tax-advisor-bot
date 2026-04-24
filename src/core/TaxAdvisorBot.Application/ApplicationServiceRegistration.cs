using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TaxAdvisorBot.Application.Options;

namespace TaxAdvisorBot.Application;

/// <summary>
/// IServiceCollection extensions for registering Application-layer options with validation.
/// </summary>
public static class ApplicationServiceRegistration
{
    /// <summary>
    /// Registers all IOptions configuration models with data annotation validation.
    /// </summary>
    public static IHostApplicationBuilder AddApplicationOptions(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<AzureAIOptions>()
            .BindConfiguration(AzureAIOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<QdrantOptions>()
            .BindConfiguration(QdrantOptions.SectionName)
            .Configure<IConfiguration>((options, config) =>
            {
                // Aspire injects the connection string as ConnectionStrings:qdrant
                var connectionString = config.GetConnectionString("qdrant");
                if (!string.IsNullOrEmpty(connectionString) && string.IsNullOrEmpty(options.ConnectionString))
                {
                    options.ConnectionString = connectionString;
                }
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddOptions<LegalSourcesOptions>()
            .BindConfiguration(LegalSourcesOptions.SectionName);

        return builder;
    }
}
