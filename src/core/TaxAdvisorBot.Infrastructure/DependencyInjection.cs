using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Qdrant.Client;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Application.Options;
using TaxAdvisorBot.Infrastructure.AI;
using TaxAdvisorBot.Infrastructure.Search;

namespace TaxAdvisorBot.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddRedisDistributedCache("cache");

        builder.Services.AddSemanticKernel();

        // Qdrant client — parse connection string for endpoint and API key
        builder.Services.AddSingleton<QdrantClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<QdrantOptions>>().Value;
            var logger = sp.GetRequiredService<ILogger<QdrantClient>>();

            logger.LogInformation("Qdrant connection string: {ConnectionString}",
                options.ConnectionString.Length > 20 ? options.ConnectionString[..20] + "..." : options.ConnectionString);

            var parts = options.ConnectionString.Split(';')
                .Select(p => p.Split('=', 2))
                .Where(p => p.Length == 2)
                .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);

            var endpoint = parts.GetValueOrDefault("Endpoint", "http://localhost:6333");
            var apiKey = parts.GetValueOrDefault("Key");

            logger.LogInformation("Qdrant endpoint: {Endpoint}, apiKey present: {HasKey}", endpoint, apiKey is not null);

            var uri = new Uri(endpoint);
            return new QdrantClient(uri.Host, uri.Port, apiKey: apiKey ?? "", https: uri.Scheme == "https");
        });

        // Embedding service — wraps AI embedding generation
        builder.Services.AddSingleton<EmbeddingService>();

        // Register Azure OpenAI embeddings via Semantic Kernel
#pragma warning disable SKEXP0010 // Embedding API is experimental
        builder.Services.AddTransient<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureAIOptions>>().Value;
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddAzureOpenAIEmbeddingGenerator(
                deploymentName: options.EmbeddingDeploymentName,
                endpoint: options.Endpoint,
                apiKey: options.ApiKey);
            var kernel = kernelBuilder.Build();
            return kernel.GetRequiredService<Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>>();
        });
#pragma warning restore SKEXP0010

        // Legal search
        builder.Services.AddSingleton<ILegalSearchService, QdrantLegalSearchService>();

        // Content extraction pipeline (HTML → strip tags, PDF → PdfPig, plain text → passthrough)
        builder.Services.AddSingleton<ContentExtractor>();

        // Legal ingestion (real-time, per-source)
        builder.Services.AddHttpClient<ILegalIngestionService, LegalIngestionService>();

        // Batch legal ingestion (all sources via Azure OpenAI Batch API)
        builder.Services.AddHttpClient<BatchLegalIngestionService>();

        return builder;
    }
}
