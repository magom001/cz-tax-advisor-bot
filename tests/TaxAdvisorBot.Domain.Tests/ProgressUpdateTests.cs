using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Domain.Tests;

public sealed class ProgressUpdateTests
{
    [Fact]
    public void Record_WithStepNameOnly_HasNullOptionalFields()
    {
        var update = new ProgressUpdate("Extracting document");

        Assert.Equal("Extracting document", update.StepName);
        Assert.Null(update.PercentComplete);
        Assert.Null(update.Message);
    }

    [Fact]
    public void Record_WithAllFields_StoresValues()
    {
        var update = new ProgressUpdate("Searching legal text", 50, "Found 3 relevant paragraphs");

        Assert.Equal("Searching legal text", update.StepName);
        Assert.Equal(50, update.PercentComplete);
        Assert.Equal("Found 3 relevant paragraphs", update.Message);
    }

    [Fact]
    public void Record_Equality_WorksByValue()
    {
        var a = new ProgressUpdate("Step", 100, "Done");
        var b = new ProgressUpdate("Step", 100, "Done");

        Assert.Equal(a, b);
    }
}
