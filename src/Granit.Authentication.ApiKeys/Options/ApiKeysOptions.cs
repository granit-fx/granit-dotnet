using System.ComponentModel.DataAnnotations;

namespace Granit.Authentication.ApiKeys.Options;

/// <summary>
/// Module-level options for <c>Granit.Authentication.ApiKeys</c>. Distinct from
/// <see cref="ApiKeyOptions"/> (which is the ASP.NET authentication scheme options
/// bag) — these settings tune background behaviour shared across the module
/// (scanner cadence, lead time, dedupe window).
/// </summary>
public sealed class ApiKeysOptions
{
    /// <summary>
    /// Default configuration section: <c>"Granit:ApiKeys"</c>.
    /// </summary>
    public const string SectionName = "Granit:ApiKeys";

    /// <summary>
    /// Number of days before <see cref="Domain.ApiKeyEntry.ExpiresAt"/> at which the
    /// scanner starts emitting <c>ApiKeyExpiringSoonEto</c>. Default: 14 days, which
    /// gives administrators a full sprint to rotate. Range: 1–90.
    /// </summary>
    [Range(1, 90)]
    public int ExpirationLeadTimeDays { get; set; } = 14;
}
