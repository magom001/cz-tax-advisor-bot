using System.ComponentModel.DataAnnotations;
using TaxAdvisorBot.Application.Options;

namespace TaxAdvisorBot.Application.Tests;

public sealed class AzureAIOptionsTests
{
    private static List<ValidationResult> Validate(AzureAIOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_Options_PassValidation()
    {
        var options = new AzureAIOptions
        {
            Endpoint = "https://my-ai.openai.azure.com/",
            ApiKey = "test-key-123",
            ChatDeploymentName = "gpt-4.1",
            FastChatDeploymentName = "gpt-4.1-mini",
            EmbeddingDeploymentName = "text-embedding-ada-002",
        };

        var results = Validate(options);

        Assert.Empty(results);
    }

    [Fact]
    public void Missing_Endpoint_FailsValidation()
    {
        var options = new AzureAIOptions
        {
            ApiKey = "test-key",
            ChatDeploymentName = "gpt-4.1",
            FastChatDeploymentName = "gpt-4.1-mini",
            EmbeddingDeploymentName = "embed",
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AzureAIOptions.Endpoint)));
    }

    [Fact]
    public void Invalid_Endpoint_Url_FailsValidation()
    {
        var options = new AzureAIOptions
        {
            Endpoint = "not-a-url",
            ApiKey = "test-key",
            ChatDeploymentName = "gpt-4.1",
            FastChatDeploymentName = "gpt-4.1-mini",
            EmbeddingDeploymentName = "embed",
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AzureAIOptions.Endpoint)));
    }

    [Fact]
    public void Missing_ApiKey_FailsValidation()
    {
        var options = new AzureAIOptions
        {
            Endpoint = "https://my-ai.openai.azure.com/",
            ChatDeploymentName = "gpt-4.1",
            FastChatDeploymentName = "gpt-4.1-mini",
            EmbeddingDeploymentName = "embed",
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AzureAIOptions.ApiKey)));
    }

    [Fact]
    public void Missing_ChatDeploymentName_FailsValidation()
    {
        var options = new AzureAIOptions
        {
            Endpoint = "https://my-ai.openai.azure.com/",
            ApiKey = "test-key",
            FastChatDeploymentName = "gpt-4.1-mini",
            EmbeddingDeploymentName = "embed",
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AzureAIOptions.ChatDeploymentName)));
    }

    [Fact]
    public void Missing_FastChatDeploymentName_FailsValidation()
    {
        var options = new AzureAIOptions
        {
            Endpoint = "https://my-ai.openai.azure.com/",
            ApiKey = "test-key",
            ChatDeploymentName = "gpt-4.1",
            EmbeddingDeploymentName = "embed",
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AzureAIOptions.FastChatDeploymentName)));
    }

    [Fact]
    public void Missing_EmbeddingDeploymentName_FailsValidation()
    {
        var options = new AzureAIOptions
        {
            Endpoint = "https://my-ai.openai.azure.com/",
            ApiKey = "test-key",
            ChatDeploymentName = "gpt-4.1",
            FastChatDeploymentName = "gpt-4.1-mini",
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(AzureAIOptions.EmbeddingDeploymentName)));
    }

    [Fact]
    public void ReasoningDeploymentName_IsOptional()
    {
        var options = new AzureAIOptions
        {
            Endpoint = "https://my-ai.openai.azure.com/",
            ApiKey = "test-key",
            ChatDeploymentName = "gpt-4.1",
            FastChatDeploymentName = "gpt-4.1-mini",
            EmbeddingDeploymentName = "text-embedding-ada-002",
            ReasoningDeploymentName = null,
        };

        var results = Validate(options);

        Assert.Empty(results);
    }
}
