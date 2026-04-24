namespace TaxAdvisorBot.Domain.Models;

/// <summary>
/// A citation linking an AI response to a specific provision of Czech tax law.
/// </summary>
/// <param name="ParagraphId">The § number (e.g. "10" for §10).</param>
/// <param name="SubParagraph">Sub-paragraph reference (e.g. "odst. 1 písm. b").</param>
/// <param name="SourceUrl">URL to the authoritative source text.</param>
/// <param name="Description">Human-readable summary of the cited provision.</param>
public sealed record LegalReference(
    string ParagraphId,
    string? SubParagraph = null,
    string? SourceUrl = null,
    string? Description = null);
