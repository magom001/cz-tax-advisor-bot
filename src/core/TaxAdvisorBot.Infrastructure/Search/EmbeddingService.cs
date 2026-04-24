using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace TaxAdvisorBot.Infrastructure.Search;

/// <summary>
/// Wraps AI embedding model calls to generate vector representations of text.
/// </summary>
public sealed class EmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly ILogger<EmbeddingService> _logger;

    public EmbeddingService(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        ILogger<EmbeddingService> logger)
    {
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Generating embedding for text of length {Length}", text.Length);
        var result = await _embeddingGenerator.GenerateAsync(
            [text], cancellationToken: cancellationToken);
        return result[0].Vector;
    }
}
