using System.Collections.ObjectModel;
using System.Text.Json;
using Granit.Domain;

namespace Granit.Identity.Federated.EntityFrameworkCore.Entities;

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
/// No <c>ISoftDeletable</c>: cache entries are hard-deleted on RGPD erasure requests.
/// The audit fields on <see cref="AuditedEntity"/> satisfy ISO 27001 requirements for the cache entry itself.
/// </para>
/// </remarks>
public sealed class UserCacheEntry : AuditedEntity, IMultiTenant, IIdentityUser
{
    private IReadOnlyDictionary<string, string>? _parsedExtraProperties;
    /// <summary>User identifier in the external identity provider (e.g. Keycloak sub). Max 256 characters.</summary>
    public string ExternalUserId { get; set; } = string.Empty;

    /// <summary>Login name. Max 256 characters.</summary>
    public string? Username { get; set; }

    /// <summary>Email address. Max 512 characters.</summary>
    public string? Email { get; set; }

    /// <summary>First name. Max 256 characters.</summary>
    public string? FirstName { get; set; }

    /// <summary>Last name. Max 256 characters.</summary>
    public string? LastName { get; set; }

    /// <summary>Whether the user account is active in the identity provider.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Timestamp of the last successful sync from the identity provider.</summary>
    public DateTimeOffset LastSyncedAt { get; set; }

    /// <inheritdoc />
    public Guid? TenantId { get; set; }

    /// <summary>JSON column for extra properties from the federated provider.</summary>
    public string? ExtraPropertiesJson { get; set; }

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
    IReadOnlyDictionary<string, string> IIdentityUser.ExtraProperties =>
        _parsedExtraProperties ??= DeserializeExtraProperties();

    private ReadOnlyDictionary<string, string> DeserializeExtraProperties()
    {
        if (string.IsNullOrWhiteSpace(ExtraPropertiesJson))
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }

        Dictionary<string, string>? parsed =
            JsonSerializer.Deserialize<Dictionary<string, string>>(ExtraPropertiesJson);

        return parsed is { Count: > 0 }
            ? new ReadOnlyDictionary<string, string>(parsed)
            : ReadOnlyDictionary<string, string>.Empty;
    }
}
