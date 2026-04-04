namespace Granit.Tax.Internal.Options;

/// <summary>
/// Configuration for EU VAT rate overrides.
/// </summary>
public sealed class EuVatRateOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Tax:Internal:Rates";

    /// <summary>
    /// Standard rate overrides by country code (e.g., <c>{"FI": 0.255}</c>).
    /// Overrides the built-in defaults from <c>EuVatRateDefaults</c>.
    /// </summary>
    public Dictionary<string, decimal> StandardRates { get; set; } = [];

    /// <summary>Reduced rate overrides by country code.</summary>
    public Dictionary<string, decimal>? ReducedRates { get; set; }
}
