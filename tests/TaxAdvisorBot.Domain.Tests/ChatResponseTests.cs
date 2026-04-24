using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Domain.Tests;

public sealed class ChatResponseTests
{
    [Fact]
    public void Record_StoresAnswerAndCitations()
    {
        var citations = new List<LegalReference>
        {
            new("10", Description: "Other income"),
            new("38f", Description: "Tax credit method"),
        };

        var response = new ChatResponse("Your RSU income falls under §10.", citations, 0.95);

        Assert.Equal("Your RSU income falls under §10.", response.AnswerText);
        Assert.Equal(2, response.Citations.Count);
        Assert.Equal(0.95, response.ConfidenceScore);
    }

    [Fact]
    public void Record_WithNullConfidence_IsValid()
    {
        var response = new ChatResponse("Answer", []);

        Assert.Null(response.ConfidenceScore);
        Assert.Empty(response.Citations);
    }

    [Fact]
    public void Record_Equality_WorksByValue()
    {
        var citations = new List<LegalReference> { new("10") };
        var a = new ChatResponse("Answer", citations, 0.9);
        var b = new ChatResponse("Answer", citations, 0.9);

        Assert.Equal(a, b);
    }
}
