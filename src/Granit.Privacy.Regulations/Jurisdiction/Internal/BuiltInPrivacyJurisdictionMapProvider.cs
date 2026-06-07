namespace Granit.Privacy.Regulations.Jurisdiction.Internal;

/// <summary>
/// Built-in ISO 3166 → regulation mapping for all Tier 1 and Tier 2 regulations.
/// </summary>
internal sealed class BuiltInPrivacyJurisdictionMapProvider : IPrivacyJurisdictionMapProvider
{
    // EU member states (ISO 3166-1 alpha-2) — all subject to EU GDPR
    private static readonly string[] EuMemberStates =
    [
        "AT", "BE", "BG", "CY", "CZ", "DE", "DK", "EE", "ES", "FI",
        "FR", "GR", "HR", "HU", "IE", "IT", "LT", "LU", "LV", "MT",
        "NL", "PL", "PT", "RO", "SE", "SI", "SK",
    ];

    public void Define(
        IDictionary<string, IReadOnlyList<PrivacyRegulation>> countryMap,
        IDictionary<string, IReadOnlyList<PrivacyRegulation>> regionMap)
    {
        // ── EU member states → EU GDPR ────────────────────────────────────
        foreach (string code in EuMemberStates)
        {
            countryMap[code] = [PrivacyRegulation.EuGdpr];
        }

        // ── Tier 1 country-level mappings ─────────────────────────────────
        countryMap["GB"] = [PrivacyRegulation.UkGdpr];
        countryMap["BR"] = [PrivacyRegulation.BrLgpd];
        countryMap["CH"] = [PrivacyRegulation.ChNfadp, PrivacyRegulation.EuGdpr]; // CH processes EU data subjects
        countryMap["CA"] = [PrivacyRegulation.CaPipeda];

        // US: no federal general privacy law — empty at country level
        countryMap["US"] = [];

        // ── Tier 1 region-level mappings ──────────────────────────────────
        regionMap["CA-QC"] = [PrivacyRegulation.CaQuebec25, PrivacyRegulation.CaPipeda]; // Quebec Law 25 + PIPEDA
        regionMap["CA-BC"] = [PrivacyRegulation.CaPipeda]; // BC PIPA is advisory; PIPEDA applies for cross-border
        regionMap["CA-AB"] = [PrivacyRegulation.CaPipeda]; // AB PIPA — same note
        regionMap["US-CA"] = [PrivacyRegulation.UsCcpa];   // California CCPA

        // ── Tier 2 country-level mappings ─────────────────────────────────
        countryMap["CN"] = [PrivacyRegulation.CnPipl];
        countryMap["IN"] = [PrivacyRegulation.InDpdpa];
        countryMap["JP"] = [PrivacyRegulation.JpAppi];
        countryMap["KR"] = [PrivacyRegulation.KrPipa];
        countryMap["AU"] = [PrivacyRegulation.AuPrivacyAct];
        countryMap["ZA"] = [PrivacyRegulation.ZaPopia];
        countryMap["TH"] = [PrivacyRegulation.ThPdpa];
    }
}
