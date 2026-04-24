using TaxAdvisorBot.Domain.Enums;
using TaxAdvisorBot.Domain.Models;

namespace TaxAdvisorBot.Domain.Tests;

public sealed class TaxDocumentContextTests
{
    [Fact]
    public void NewContext_HasUnknownDocumentType()
    {
        var context = new TaxDocumentContext();

        Assert.Equal(DocumentType.Unknown, context.DocumentType);
    }

    [Fact]
    public void ConfidenceScore_DefaultsToZero()
    {
        var context = new TaxDocumentContext();

        Assert.Equal(0.0, context.ConfidenceScore);
    }

    [Fact]
    public void Context_WithAllFields_StoresValues()
    {
        var context = new TaxDocumentContext
        {
            DocumentType = DocumentType.BrokerageStatement,
            FileName = "schwab-2025.pdf",
            RelevantSection = TaxSection.Other,
            IncomeAmount = 50_000m,
            ExpenseAmount = 10_000m,
            TaxWithheld = 7_500m,
            CurrencyCode = "USD",
            DocumentDate = new DateOnly(2025, 12, 31),
            TaxYear = 2025,
            ConfidenceScore = 0.92,
            RawText = "Proceeds: $50,000"
        };

        Assert.Equal(DocumentType.BrokerageStatement, context.DocumentType);
        Assert.Equal("schwab-2025.pdf", context.FileName);
        Assert.Equal(TaxSection.Other, context.RelevantSection);
        Assert.Equal(50_000m, context.IncomeAmount);
        Assert.Equal(10_000m, context.ExpenseAmount);
        Assert.Equal(7_500m, context.TaxWithheld);
        Assert.Equal("USD", context.CurrencyCode);
        Assert.Equal(new DateOnly(2025, 12, 31), context.DocumentDate);
        Assert.Equal(2025, context.TaxYear);
        Assert.Equal(0.92, context.ConfidenceScore);
        Assert.Equal("Proceeds: $50,000", context.RawText);
    }
}
