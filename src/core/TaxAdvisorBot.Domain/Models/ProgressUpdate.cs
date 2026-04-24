namespace TaxAdvisorBot.Domain.Models;

/// <summary>
/// Payload sent to clients during long-running operations to report progress.
/// </summary>
/// <param name="StepName">Name of the current processing step (e.g. "Extracting document", "Searching legal text").</param>
/// <param name="PercentComplete">Progress percentage (0–100). Null if indeterminate.</param>
/// <param name="Message">Human-readable status message.</param>
public sealed record ProgressUpdate(
    string StepName,
    int? PercentComplete = null,
    string? Message = null);
