using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using TaxAdvisorBot.Application.Options;
using TaxAdvisorBot.Infrastructure.AI.Plugins;

namespace TaxAdvisorBot.Infrastructure.AI;

/// <summary>
/// Configures the Semantic Kernel instance with Azure AI models and native C# plugins.
/// </summary>
public static class SemanticKernelRegistration
{
    /// <summary>
    /// Registers the Semantic Kernel with Azure OpenAI chat completion, and all native plugins.
    /// </summary>
    public static IServiceCollection AddSemanticKernel(this IServiceCollection services)
    {
        services.AddSingleton<TaxCalculationPlugin>();
        services.AddSingleton<TaxValidationPlugin>();
        services.AddSingleton<ExchangeRatePlugin>();

        // Override the kernel configuration with a factory that reads IOptions
        services.AddTransient<Kernel>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureAIOptions>>().Value;

            var builder = Kernel.CreateBuilder();

            // LLM calls for ingestion can take minutes — use a long-lived HttpClient
            var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };

            builder.AddAzureOpenAIChatCompletion(
                deploymentName: options.ChatDeploymentName,
                endpoint: options.Endpoint,
                apiKey: options.ApiKey,
                serviceId: "chat",
                httpClient: httpClient);

            if (!string.IsNullOrEmpty(options.FastChatDeploymentName))
            {
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: options.FastChatDeploymentName,
                    endpoint: options.Endpoint,
                    apiKey: options.ApiKey,
                    serviceId: "fast-chat",
                    httpClient: httpClient);
            }

            if (!string.IsNullOrEmpty(options.ReasoningDeploymentName))
            {
                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: options.ReasoningDeploymentName,
                    endpoint: options.Endpoint,
                    apiKey: options.ApiKey,
                    serviceId: "reasoning",
                    httpClient: httpClient);
            }

            var kernel = builder.Build();

            kernel.Plugins.AddFromObject(sp.GetRequiredService<TaxCalculationPlugin>(), "TaxCalculation");
            kernel.Plugins.AddFromObject(sp.GetRequiredService<TaxValidationPlugin>(), "TaxValidation");
            kernel.Plugins.AddFromObject(sp.GetRequiredService<ExchangeRatePlugin>(), "ExchangeRate");

            return kernel;
        });

        return services;
    }
}
