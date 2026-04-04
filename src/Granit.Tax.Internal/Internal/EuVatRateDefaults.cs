using System.Collections.Frozen;

namespace Granit.Tax.Internal.Internal;

/// <summary>
/// Default EU VAT standard rates for all 27 member states (as of 2024).
/// </summary>
/// <remarks>
/// Rates are expressed as decimals (e.g., 0.21 for 21%).
/// These defaults can be overridden via configuration or DB-managed <c>TaxRateOverride</c>.
/// Source: European Commission — VAT rates applied in the Member States of the EU.
/// </remarks>
internal static class EuVatRateDefaults
{
    internal static readonly FrozenDictionary<string, decimal> StandardRates =
        new Dictionary<string, decimal>
        {
            ["AT"] = 0.20m,  // Austria
            ["BE"] = 0.21m,  // Belgium
            ["BG"] = 0.20m,  // Bulgaria
            ["HR"] = 0.25m,  // Croatia
            ["CY"] = 0.19m,  // Cyprus
            ["CZ"] = 0.21m,  // Czech Republic
            ["DK"] = 0.25m,  // Denmark
            ["EE"] = 0.22m,  // Estonia
            ["FI"] = 0.255m, // Finland (25.5% since 2024)
            ["FR"] = 0.20m,  // France
            ["DE"] = 0.19m,  // Germany
            ["GR"] = 0.24m,  // Greece
            ["HU"] = 0.27m,  // Hungary
            ["IE"] = 0.23m,  // Ireland
            ["IT"] = 0.22m,  // Italy
            ["LV"] = 0.21m,  // Latvia
            ["LT"] = 0.21m,  // Lithuania
            ["LU"] = 0.17m,  // Luxembourg
            ["MT"] = 0.18m,  // Malta
            ["NL"] = 0.21m,  // Netherlands
            ["PL"] = 0.23m,  // Poland
            ["PT"] = 0.23m,  // Portugal
            ["RO"] = 0.19m,  // Romania
            ["SK"] = 0.23m,  // Slovakia
            ["SI"] = 0.22m,  // Slovenia
            ["ES"] = 0.21m,  // Spain
            ["SE"] = 0.25m,  // Sweden
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
}
