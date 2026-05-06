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

    /// <summary>
    /// Server-side secret mixed into the API key hash via HMAC-SHA256. Without
    /// this pepper, a database dump alone is sufficient to enumerate valid keys
    /// against any precomputed table; with it, an attacker also needs the
    /// pepper from the configuration / secret store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Format: base64-encoded, must decode to at least 32 bytes (256 bits).
    /// Source it from a secret manager (Azure Key Vault, HashiCorp Vault,
    /// Kubernetes Secret, AWS Secrets Manager) — never commit it.
    /// </para>
    /// <para>
    /// When unset, new keys are stored with the legacy <c>v1$&lt;sha256&gt;</c>
    /// format. When set, new keys are stored with <c>v2$&lt;hmac-sha256&gt;</c>;
    /// existing v1 keys remain valid (the auth handler falls back from v2 to
    /// v1 lookup) until rotated. Configure the pepper as soon as practical
    /// to harden new key issuance.
    /// </para>
    /// </remarks>
    public string? Pepper { get; set; }
}
