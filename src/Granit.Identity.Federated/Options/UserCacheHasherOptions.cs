namespace Granit.Identity.Federated.Options;

/// <summary>
/// Configuration for the user-cache lookup hasher used when PII is encrypted at rest.
/// </summary>
/// <remarks>
/// <para>
/// when <see cref="Granit.Identity.Federated.Domain.UserCacheEntry.Email"/>
/// is encrypted, it can no longer be <c>LIKE</c>-searched. The admin path looks up
/// users via <c>EmailHash = HMAC-SHA256(pepper, lowered-email)</c> instead. The
/// pepper configured here MUST:
/// </para>
/// <list type="bullet">
///   <item>Be at least 32 bytes of high-entropy random data</item>
///   <item>Be stored separately from the encryption key ring (so the two rotate
///   independently)</item>
///   <item>Be loaded from a secret store (Vault, Azure Key Vault, environment
///   variable) — never baked into <c>appsettings.json</c> in source control</item>
/// </list>
/// </remarks>
public sealed class UserCacheHasherOptions
{
    /// <summary>Configuration section name for binding from <c>appsettings.json</c>.</summary>
    public const string SectionName = "Identity:UserCacheHasher";

    /// <summary>
    /// HMAC pepper for email lookups. Accepts either a hex string (e.g. 64 hex chars
    /// for 32 bytes) or a raw UTF-8 string. Required — startup fails if unset.
    /// </summary>
    public string EmailLookupPepper { get; set; } = string.Empty;
}
