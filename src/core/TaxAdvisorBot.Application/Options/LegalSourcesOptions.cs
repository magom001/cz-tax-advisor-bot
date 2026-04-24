using System.ComponentModel.DataAnnotations;

namespace TaxAdvisorBot.Application.Options;

/// <summary>
/// Configuration for legal text sources to ingest into the knowledge base.
/// </summary>
public sealed class LegalSourcesOptions
{
    public const string SectionName = "LegalSources";

    /// <summary>List of legal sources available for ingestion.</summary>
    public List<LegalSource> Sources { get; set; } = [];
}

/// <summary>
/// A single legal source definition.
/// </summary>
public sealed class LegalSource
{
    /// <summary>Display name (e.g. "Income Tax Act").</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>URL to scrape.</summary>
    [Required, Url]
    public string Url { get; set; } = string.Empty;

    /// <summary>Document type tag for metadata (e.g. "Act", "Instruction").</summary>
    [Required]
    public string DocumentType { get; set; } = "Act";

    /// <summary>Short description.</summary>
    public string? Description { get; set; }
}
