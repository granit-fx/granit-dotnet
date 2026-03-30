namespace Granit.Privacy.Regulations;

/// <summary>
/// Describes how consent is collected in a given jurisdiction.
/// </summary>
public enum ConsentModel
{
    /// <summary>User must explicitly opt in before processing (GDPR, LGPD, PIPL, DPDPA).</summary>
    OptIn = 0,

    /// <summary>Processing allowed by default; user can opt out (CCPA).</summary>
    OptOut = 1,

    /// <summary>Opt-in for sensitive data, opt-out for non-sensitive (some US states).</summary>
    Hybrid = 2,

    /// <summary>No specific consent requirement for this jurisdiction.</summary>
    None = 3,
}
