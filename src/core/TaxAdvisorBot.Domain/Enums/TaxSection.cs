namespace TaxAdvisorBot.Domain.Enums;

/// <summary>
/// Sections of the Czech Income Tax Act (ZDP 586/1992) that classify income types.
/// </summary>
public enum TaxSection
{
    /// <summary>§6 — Income from employment (závislá činnost).</summary>
    Employment = 6,

    /// <summary>§7 — Income from self-employment (samostatná činnost).</summary>
    SelfEmployment = 7,

    /// <summary>§8 — Income from capital / capital gains (kapitálový majetek).</summary>
    CapitalGains = 8,

    /// <summary>§9 — Income from rental (příjmy z nájmu).</summary>
    Rental = 9,

    /// <summary>§10 — Other income (ostatní příjmy), e.g. RSUs, crypto, occasional sales.</summary>
    Other = 10
}
