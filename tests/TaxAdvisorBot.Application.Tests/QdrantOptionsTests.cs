using System.ComponentModel.DataAnnotations;
using TaxAdvisorBot.Application.Options;

namespace TaxAdvisorBot.Application.Tests;

public sealed class QdrantOptionsTests
{
    private static List<ValidationResult> Validate(QdrantOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Valid_Options_PassValidation()
    {
        var options = new QdrantOptions
        {
            Endpoint = "http://localhost:6333",
            CollectionName = "czech-tax-2025",
            VectorSize = 1536,
        };

        var results = Validate(options);

        Assert.Empty(results);
    }

    [Fact]
    public void Missing_Endpoint_FailsValidation()
    {
        var options = new QdrantOptions
        {
            CollectionName = "czech-tax",
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(QdrantOptions.Endpoint)));
    }

    [Fact]
    public void VectorSize_Zero_FailsValidation()
    {
        var options = new QdrantOptions
        {
            Endpoint = "http://localhost:6333",
            VectorSize = 0,
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(QdrantOptions.VectorSize)));
    }

    [Fact]
    public void VectorSize_Negative_FailsValidation()
    {
        var options = new QdrantOptions
        {
            Endpoint = "http://localhost:6333",
            VectorSize = -1,
        };

        var results = Validate(options);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(QdrantOptions.VectorSize)));
    }

    [Fact]
    public void Defaults_HaveValidCollectionName()
    {
        var options = new QdrantOptions
        {
            Endpoint = "http://localhost:6333",
        };

        Assert.Equal("czech-tax", options.CollectionName);
        Assert.Equal(1536, options.VectorSize);
    }
}
