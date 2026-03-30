namespace Granit.Privacy.Regulations.Options;

/// <summary>
/// Configuration options for the privacy regulations module.
/// Bound to the <c>Privacy:Regulations</c> configuration section.
/// </summary>
public sealed class PrivacyRegulationsOptions
{
    /// <summary>Section key in the configuration.</summary>
    public const string SectionName = "Privacy:Regulations";

    /// <summary>
    /// Default regulation code applied when no per-tenant override is configured.
    /// Must match a registered <see cref="PrivacyRegulation"/> value (e.g., <c>"EU_GDPR"</c>).
    /// </summary>
    public string? DefaultRegulation { get; set; }

    /// <summary>
    /// Per-tenant regulation overrides. Key = tenant ID (string), value = regulation code.
    /// </summary>
    /// <example>
    /// <code>
    /// "TenantRegulations": {
    ///   "tenant-brazil": "BR_LGPD",
    ///   "tenant-california": "US_CCPA"
    /// }
    /// </code>
    /// </example>
    public Dictionary<string, string> TenantRegulations { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
