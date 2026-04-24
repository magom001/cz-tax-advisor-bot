namespace TaxAdvisorBot.Domain.Enums;

/// <summary>
/// Represents the current status of a tax filing.
/// </summary>
public enum FilingStatus
{
    /// <summary>Initial state — filing has been created but not yet validated.</summary>
    Draft,

    /// <summary>Validation detected missing or incomplete information.</summary>
    MissingInfo,

    /// <summary>All required fields are present; awaiting user review.</summary>
    ReadyForReview,

    /// <summary>Filing is finalized and ready for submission.</summary>
    Complete
}
