namespace TaxAdvisorBot.Domain.Models;

/// <summary>
/// AI-generated response returned to the user, including citations to Czech tax law.
/// </summary>
/// <param name="AnswerText">The main text of the AI response.</param>
/// <param name="Citations">Legal references cited in this response.</param>
/// <param name="ConfidenceScore">Model confidence in the answer (0.0–1.0). Null if not available.</param>
public sealed record ChatResponse(
    string AnswerText,
    IReadOnlyList<LegalReference> Citations,
    double? ConfidenceScore = null);
