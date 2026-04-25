using System.Collections.ObjectModel;
using System.Text.Json;
using Granit.DataProtection;
using Granit.Domain;
using Granit.Encryption.EntityFrameworkCore;

namespace Granit.Identity.Federated.Domain;

/// <summary>
/// Local cache entry for an external identity provider user.
/// Read-only mirror of the identity provider data, synced via cache-aside, login-time, or webhook strategies.
/// </summary>
/// <remarks>
/// <para>
/// Inherits <see cref="AuditedEntity"/> for ISO 27001 audit trail (CreatedAt, CreatedBy, ModifiedAt, ModifiedBy).
/// Implements <see cref="IMultiTenant"/> for tenant isolation — the same external user may have
/// a cache entry per tenant in shared-realm deployments.
/// </para>
/// <para>
/// No <c>ISoftDeletable</c>: cache entries are hard-deleted on GDPR erasure requests.
/// The audit fields on <see cref="AuditedEntity"/> satisfy ISO 27001 requirements for the cache entry itself.
/// </para>
/// </remarks>
public sealed class UserCacheEntry : AuditedEntity, IMultiTenant, IIdentityUser
{
    private IReadOnlyDictionary<string, string>? _parsedMetadata;
    /// <summary>User identifier in the external identity provider (e.g. Keycloak sub). Max 256 characters.</summary>
    public string ExternalUserId { get; set; } = string.Empty;

    /// <summary>Login name. Max 256 characters. Encrypted at rest.</summary>
    [SensitiveData]
    [Encrypted]
    public string? Username { get; set; }

    /// <summary>
    /// Email address. Max 512 characters. Encrypted at rest — the
    /// admin search path looks up users via <see cref="EmailHash"/> instead of
    /// scanning the ciphertext.
    /// </summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    [Encrypted]
    public string? Email { get; set; }

    /// <summary>
    /// HMAC-SHA256 of <c>email.ToLowerInvariant()</c> computed with a dedicated
    /// lookup pepper. Indexed alongside <see cref="TenantId"/> for exact-match
    /// search on encrypted data. Never surfaced to callers.
    /// </summary>
    public string? EmailHash { get; set; }

    /// <summary>First name. Max 256 characters. Encrypted at rest.</summary>
    [SensitiveData]
    [Encrypted]
    public string? FirstName { get; set; }

    /// <summary>Last name. Max 256 characters. Encrypted at rest.</summary>
    [SensitiveData]
    [Encrypted]
    public string? LastName { get; set; }

    /// <summary>Whether the user account is active in the identity provider.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Timestamp of the last successful sync from the identity provider.</summary>
    public DateTimeOffset LastSyncedAt { get; set; }

    /// <inheritdoc />
    public Guid? TenantId { get; set; }

    /// <summary>JSON column for extra properties from the federated provider.</summary>
    public string? MetadataJson { get; set; }

    // ──── IIdentityUser (explicit implementation) ────

    /// <inheritdoc/>
    string IIdentityUser.UserId => ExternalUserId;

    /// <inheritdoc/>
    string? IIdentityUser.Username => Username;

    /// <inheritdoc/>
    string? IIdentityUser.Email => Email;

    /// <inheritdoc/>
    bool IIdentityUser.Enabled => Enabled;

    /// <inheritdoc/>
    IReadOnlyDictionary<string, string> IIdentityUser.Metadata =>
        _parsedMetadata ??= DeserializeMetadata();

    private ReadOnlyDictionary<string, string> DeserializeMetadata()
    {
        if (string.IsNullOrWhiteSpace(MetadataJson))
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }

        Dictionary<string, string>? parsed =
            JsonSerializer.Deserialize<Dictionary<string, string>>(MetadataJson);

        return parsed is { Count: > 0 }
            ? new ReadOnlyDictionary<string, string>(parsed)
            : ReadOnlyDictionary<string, string>.Empty;
    }
}
