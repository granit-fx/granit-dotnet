using System.Collections.Frozen;

namespace Granit.Tax.Internal;

/// <summary>
/// EU member state country codes (ISO 3166-1 alpha-2).
/// </summary>
/// <remarks>
/// <para>27 member states as of 2024. Excludes GB and XI (Northern Ireland)
/// — for digital services (SaaS), Northern Ireland follows UK rules post-Brexit
/// (Windsor Framework only applies to physical goods).</para>
/// <para>Greece uses EL in tax context (VIES) but GR in ISO 3166-1.
/// Both are accepted by <see cref="IsEuMember"/>.</para>
/// </remarks>
public static class EuCountries
{
    /// <summary>The 27 EU member state codes (ISO 3166-1 alpha-2).</summary>
    public static readonly FrozenSet<string> MemberStates = FrozenSet.ToFrozenSet(
    [
        "AT", // Austria
        "BE", // Belgium
        "BG", // Bulgaria
        "HR", // Croatia
        "CY", // Cyprus
        "CZ", // Czech Republic
        "DK", // Denmark
        "EE", // Estonia
        "FI", // Finland
        "FR", // France
        "DE", // Germany
        "GR", // Greece (ISO 3166-1)
        "HU", // Hungary
        "IE", // Ireland
        "IT", // Italy
        "LV", // Latvia
        "LT", // Lithuania
        "LU", // Luxembourg
        "MT", // Malta
        "NL", // Netherlands
        "PL", // Poland
        "PT", // Portugal
        "RO", // Romania
        "SK", // Slovakia
        "SI", // Slovenia
        "ES", // Spain
        "SE", // Sweden
    ], StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns whether a country code is an EU member state.</summary>
    /// <remarks>Accepts both GR and EL for Greece.</remarks>
    public static bool IsEuMember(string countryCode) =>
        MemberStates.Contains(countryCode)
        || string.Equals(countryCode, "EL", StringComparison.OrdinalIgnoreCase);
}
