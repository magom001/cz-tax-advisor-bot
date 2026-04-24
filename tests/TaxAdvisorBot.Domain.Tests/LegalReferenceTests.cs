using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Domain.Tests;

public sealed class LegalReferenceTests
{
    [Fact]
    public void Record_WithRequiredFields_CreatesSuccessfully()
    {
        var reference = new LegalReference("10");

        Assert.Equal("10", reference.ParagraphId);
        Assert.Null(reference.SubParagraph);
        Assert.Null(reference.SourceUrl);
        Assert.Null(reference.Description);
    }

    [Fact]
    public void Record_WithAllFields_StoresValues()
    {
        var reference = new LegalReference(
            ParagraphId: "38f",
            SubParagraph: "odst. 1",
            SourceUrl: "https://zakonyprolidi.cz/cs/1992-586#p38f",
            Description: "Credit method for tax paid abroad");

        Assert.Equal("38f", reference.ParagraphId);
        Assert.Equal("odst. 1", reference.SubParagraph);
        Assert.Equal("https://zakonyprolidi.cz/cs/1992-586#p38f", reference.SourceUrl);
        Assert.Equal("Credit method for tax paid abroad", reference.Description);
    }

    [Fact]
    public void Record_Equality_WorksByValue()
    {
        var a = new LegalReference("10", "odst. 1");
        var b = new LegalReference("10", "odst. 1");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Record_Inequality_DifferentParagraph()
    {
        var a = new LegalReference("10");
        var b = new LegalReference("6");

        Assert.NotEqual(a, b);
    }
}
