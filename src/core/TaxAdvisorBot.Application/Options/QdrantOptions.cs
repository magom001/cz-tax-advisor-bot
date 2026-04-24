using System.ComponentModel.DataAnnotations;

namespace TaxAdvisorBot.Application.Options;

/// <summary>
/// Configuration for Qdrant vector database connection.
/// </summary>
public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";

    /// <summary>Qdrant HTTP endpoint URL.</summary>
    [Required, Url]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Name of the collection to use for legal text search.</summary>
    [Required]
    public string CollectionName { get; set; } = "czech-tax";

    /// <summary>Dimensionality of the embedding vectors.</summary>
    [Range(1, 10000)]
    public int VectorSize { get; set; } = 1536;
}
