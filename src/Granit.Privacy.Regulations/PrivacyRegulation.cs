using Granit.Domain;

namespace Granit.Privacy.Regulations;

/// <summary>
/// Identifies a privacy regulation. Open type based on <see cref="SingleValueObject{T}"/> —
/// new regulations can be added without modifying the framework.
/// </summary>
/// <remarks>
/// Tier 1 and Tier 2 regulations are declared as static fields.
/// Tier 3 (custom) regulations are created via <see cref="Create"/>.
/// </remarks>
public sealed class PrivacyRegulation : SingleValueObject<string>
{
    /// <inheritdoc />
    public override required string Value { get; init; }

    private PrivacyRegulation() { }

    /// <summary>Creates a custom regulation identifier (Tier 3).</summary>
    /// <param name="code">Regulation code (e.g., <c>"SA_PDPL"</c>). Must not be null or whitespace.</param>
    public static PrivacyRegulation Create(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        return new PrivacyRegulation { Value = code };
    }

    /// <summary>Converts a <see cref="PrivacyRegulation"/> to its string code.</summary>
    public static implicit operator string(PrivacyRegulation regulation) => regulation.Value;

    /// <summary>Converts a string code to a <see cref="PrivacyRegulation"/>.</summary>
    public static implicit operator PrivacyRegulation(string code) => Create(code);

    // ── Tier 1 — Built-in, fully supported ─────────────────────────────

    /// <summary>European Union General Data Protection Regulation.</summary>
    public static readonly PrivacyRegulation EuGdpr = new() { Value = "EU_GDPR" };

    /// <summary>United Kingdom General Data Protection Regulation (post-Brexit).</summary>
    public static readonly PrivacyRegulation UkGdpr = new() { Value = "UK_GDPR" };

    /// <summary>Brazil Lei Geral de Proteção de Dados.</summary>
    public static readonly PrivacyRegulation BrLgpd = new() { Value = "BR_LGPD" };

    /// <summary>United States California Consumer Privacy Act / California Privacy Rights Act.</summary>
    public static readonly PrivacyRegulation UsCcpa = new() { Value = "US_CCPA" };

    /// <summary>Canada Personal Information Protection and Electronic Documents Act.</summary>
    public static readonly PrivacyRegulation CaPipeda = new() { Value = "CA_PIPEDA" };

    /// <summary>Canada Quebec Law 25 (Act respecting the protection of personal information in the private sector).</summary>
    public static readonly PrivacyRegulation CaQuebec25 = new() { Value = "CA_QUEBEC_25" };

    /// <summary>Switzerland new Federal Act on Data Protection.</summary>
    public static readonly PrivacyRegulation ChNfadp = new() { Value = "CH_NFADP" };

    // ── Tier 2 — Built-in profiles, configurable ───────────────────────

    /// <summary>China Personal Information Protection Law.</summary>
    public static readonly PrivacyRegulation CnPipl = new() { Value = "CN_PIPL" };

    /// <summary>India Digital Personal Data Protection Act.</summary>
    public static readonly PrivacyRegulation InDpdpa = new() { Value = "IN_DPDPA" };

    /// <summary>Japan Act on the Protection of Personal Information.</summary>
    public static readonly PrivacyRegulation JpAppi = new() { Value = "JP_APPI" };

    /// <summary>South Korea Personal Information Protection Act.</summary>
    public static readonly PrivacyRegulation KrPipa = new() { Value = "KR_PIPA" };

    /// <summary>Australia Privacy Act 1988 (reformed).</summary>
    public static readonly PrivacyRegulation AuPrivacyAct = new() { Value = "AU_PRIVACY_ACT" };

    /// <summary>South Africa Protection of Personal Information Act.</summary>
    public static readonly PrivacyRegulation ZaPopia = new() { Value = "ZA_POPIA" };

    /// <summary>Thailand Personal Data Protection Act.</summary>
    public static readonly PrivacyRegulation ThPdpa = new() { Value = "TH_PDPA" };
}
