using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Qdrant;
using Microsoft.SemanticKernel.Data;
using MongoDB.Driver;
using Qdrant.Client;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Application.Options;
using TaxAdvisorBot.Infrastructure.AI;
using TaxAdvisorBot.Infrastructure.ExchangeRates;
using TaxAdvisorBot.Infrastructure.Persistence;
using TaxAdvisorBot.Infrastructure.Search;

#pragma warning disable SKEXP0001 // TextSearchProvider is experimental
#pragma warning disable SKEXP0010 // Embedding API is experimental
#pragma warning disable SKEXP0020 // Qdrant vector store connector is experimental
#pragma warning disable SKEXP0130 // TextSearchProvider is experimental

namespace TaxAdvisorBot.Infrastructure;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddInfrastructureServices(this IHostApplicationBuilder builder)
    {
        builder.AddRedisDistributedCache("cache");

        // MongoDB — persistent storage for conversations, tax returns, uniform rates
        builder.AddMongoDBClient("taxadvisor");
        builder.Services.AddSingleton<MongoCollections>(sp =>
        {
            var client = sp.GetRequiredService<IMongoClient>();
            var database = client.GetDatabase("taxadvisor");
            return new MongoCollections(database);
        });

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

        // Exchange rates
        builder.Services.AddSingleton<IUniformRateRepository, MongoUniformRateRepository>();
        builder.Services.AddHttpClient<IExchangeRateService, CnbExchangeRateService>();

        // Seed uniform rates from appsettings on startup
        builder.Services.AddHostedService<UniformRateSeeder>();

        // Repositories
        builder.Services.AddSingleton<IConversationRepository, MongoConversationRepository>();
        builder.Services.AddSingleton<ITaxReturnRepository, MongoTaxReturnRepository>();

        // Legal search (legacy — direct Qdrant queries)
        builder.Services.AddSingleton<ILegalSearchService, QdrantLegalSearchService>();

        // TextSearchProvider for RAG — used by the agent to auto-retrieve legal text
        // Uses our existing QdrantLegalSearchService wrapped as ITextSearch
        builder.Services.AddSingleton<TextSearchProvider>(sp =>
        {
            var searchService = sp.GetRequiredService<ILegalSearchService>();
            var textSearch = new LegalTextSearchAdapter(searchService);

            return new TextSearchProvider(textSearch, options: new TextSearchProviderOptions
            {
                Top = 5,
                SearchTime = TextSearchProviderOptions.RagBehavior.BeforeAIInvoke,
            });
        });

        // Tax advisor agent (ChatCompletionAgent with RAG + plugins)
        builder.Services.AddSingleton<IConversationService, TaxAdvisorAgentService>();

        // Content extraction pipeline (HTML → strip tags, PDF → PdfPig, plain text → passthrough)
        builder.Services.AddSingleton<ContentExtractor>();

        // Legal ingestion (real-time, per-source)
        builder.Services.AddHttpClient<ILegalIngestionService, LegalIngestionService>();

        // Batch legal ingestion (all sources via Azure OpenAI Batch API)
        builder.Services.AddHttpClient<BatchLegalIngestionService>();

        return builder;
    }
}
