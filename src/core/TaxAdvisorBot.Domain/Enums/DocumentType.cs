namespace TaxAdvisorBot.Domain.Enums;

/// <summary>
/// Type of document extracted by the document intelligence service.
/// </summary>
public enum DocumentType
{
    /// <summary>Unknown or unrecognized document.</summary>
    Unknown,

    /// <summary>Employment income confirmation (Potvrzení o zdanitelných příjmech).</summary>
    EmploymentConfirmation,

    /// <summary>Brokerage / stock transaction report (RSU, ESPP, share sales).</summary>
    BrokerageStatement,

    /// <summary>RSU vesting confirmation from equity plan administrator.</summary>
    RsuVestingConfirmation,

    /// <summary>ESPP purchase confirmation.</summary>
    EsppPurchaseConfirmation,

    /// <summary>Foreign tax form (e.g. W-8BEN, 1042-S).</summary>
    ForeignTaxForm,

    /// <summary>Pension fund annual statement (penzijní připojištění/spoření).</summary>
    PensionFundStatement,

    /// <summary>Life insurance annual statement (životní pojištění).</summary>
    LifeInsuranceStatement,

    /// <summary>Mortgage interest confirmation from the bank.</summary>
    MortgageInterestConfirmation,

    /// <summary>Charitable donation receipt (potvrzení o daru).</summary>
    DonationReceipt,

    /// <summary>Bank statement.</summary>
    BankStatement
}
