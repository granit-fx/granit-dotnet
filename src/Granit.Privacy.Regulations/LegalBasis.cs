using Granit.Domain;

namespace Granit.Privacy.Regulations;

/// <summary>
/// Legal basis for processing personal data. Superset of GDPR Art. 6 (6 bases),
/// LGPD Art. 7 (10 bases), and PIPL-specific bases.
/// Open type — jurisdiction-specific bases can be added via <see cref="Create"/>.
/// </summary>
public sealed class LegalBasis : SingleValueObject<string>
{
    /// <inheritdoc />
    public override required string Value { get; init; }

    private LegalBasis() { }

    /// <summary>Creates a custom legal basis identifier.</summary>
    /// <param name="code">Legal basis code (e.g., <c>"CREDIT_PROTECTION"</c>).</param>
    public static LegalBasis Create(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new LegalBasis { Value = code };
    }

    /// <summary>Converts a <see cref="LegalBasis"/> to its string code.</summary>
    public static implicit operator string(LegalBasis basis) => basis.Value;

    /// <summary>Converts a string code to a <see cref="LegalBasis"/>.</summary>
    public static implicit operator LegalBasis(string code) => Create(code);

    // ── GDPR Art. 6 (also used by most regulations) ────────────────────

    /// <summary>Processing based on the data subject's consent (GDPR Art. 6(1)(a)).</summary>
    public static readonly LegalBasis Consent = new() { Value = "CONSENT" };

    /// <summary>Processing necessary for a contract with the data subject (GDPR Art. 6(1)(b)).</summary>
    public static readonly LegalBasis Contract = new() { Value = "CONTRACT" };

    /// <summary>Processing necessary for compliance with a legal obligation (GDPR Art. 6(1)(c)).</summary>
    public static readonly LegalBasis LegalObligation = new() { Value = "LEGAL_OBLIGATION" };

    /// <summary>Processing necessary to protect vital interests (GDPR Art. 6(1)(d)).</summary>
    public static readonly LegalBasis VitalInterest = new() { Value = "VITAL_INTEREST" };

    /// <summary>Processing necessary for a task carried out in the public interest (GDPR Art. 6(1)(e)).</summary>
    public static readonly LegalBasis PublicInterest = new() { Value = "PUBLIC_INTEREST" };

    /// <summary>Processing necessary for legitimate interests of the controller (GDPR Art. 6(1)(f)).</summary>
    public static readonly LegalBasis LegitimateInterest = new() { Value = "LEGITIMATE_INTEREST" };

    // ── LGPD Art. 7 additions ──────────────────────────────────────────

    /// <summary>Processing for credit protection purposes (LGPD Art. 7(X)).</summary>
    public static readonly LegalBasis CreditProtection = new() { Value = "CREDIT_PROTECTION" };

    /// <summary>Processing for health protection purposes by health authorities (LGPD Art. 7(VIII)).</summary>
    public static readonly LegalBasis HealthProtection = new() { Value = "HEALTH_PROTECTION" };

    /// <summary>Processing for studies by research bodies (LGPD Art. 7(IV)).</summary>
    public static readonly LegalBasis ResearchByStudyBodies = new() { Value = "RESEARCH" };

    /// <summary>Processing for the protection of life or physical safety (LGPD Art. 7(VII)).</summary>
    public static readonly LegalBasis LifeProtection = new() { Value = "LIFE_PROTECTION" };

    // ── PIPL-specific ──────────────────────────────────────────────────

    /// <summary>Processing necessary for human resource management (PIPL Art. 13(2)).</summary>
    public static readonly LegalBasis HumanResourceManagement = new() { Value = "HR_MANAGEMENT" };

    /// <summary>Processing necessary for performing a statutory duty or obligation (PIPL Art. 13(3)).</summary>
    public static readonly LegalBasis StatutoryDuty = new() { Value = "STATUTORY_DUTY" };
}
