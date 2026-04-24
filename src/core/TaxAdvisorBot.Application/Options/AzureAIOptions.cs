using System.ComponentModel.DataAnnotations;

namespace TaxAdvisorBot.Application.Options;

/// <summary>
/// Configuration for Azure AI Foundry model deployments.
/// </summary>
public sealed class AzureAIOptions
{
    public const string SectionName = "AzureAI";

    /// <summary>Azure AI Foundry endpoint URL.</summary>
    [Required, Url]
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>API key for the Azure AI endpoint.</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Primary chat model for complex reasoning and legal analysis (e.g. "gpt-4.1").</summary>
    [Required]
    public string ChatDeploymentName { get; set; } = string.Empty;

    /// <summary>Fast/cheap model for data extraction and simple classification (e.g. "gpt-4.1-mini").</summary>
    [Required]
    public string FastChatDeploymentName { get; set; } = string.Empty;

    /// <summary>Reasoning model for multi-step tax planning and verification (e.g. "o4-mini").</summary>
    public string? ReasoningDeploymentName { get; set; }

    /// <summary>Deployment name for the embedding model (e.g. "text-embedding-ada-002").</summary>
    [Required]
    public string EmbeddingDeploymentName { get; set; } = string.Empty;
}
