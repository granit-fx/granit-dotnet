using System.Collections.ObjectModel;
using System.Text.Json;
using Granit.Domain;
using Granit.Identity;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.Domain;

/// <summary>
/// Application user entity extending ASP.NET Core Identity's <see cref="IdentityUser{TKey}"/>
/// with Granit multi-tenancy and audit support.
/// </summary>
/// <remarks>
/// <para>
/// Implements <see cref="IMultiTenant"/> for tenant isolation. Does NOT implement
/// <c>ISoftDeletable</c> (incompatible with <c>UserManager</c>) — uses manual
/// <see cref="IsDeleted"/> / <see cref="DeletedAt"/> fields with an explicit named query filter.
/// </para>
/// <para>
/// Does NOT implement <c>IConcurrencyAware</c> — ASP.NET Core Identity manages its own
/// <see cref="IdentityUser{TKey}.ConcurrencyStamp"/> property.
/// </para>
/// <para>
/// Implements <see cref="IHasMetadata"/> via explicit interface mapping to
/// <see cref="CustomAttributesJson"/>. The generic <c>MetadataSyncInterceptor</c>
/// in <c>Granit.Persistence.EntityFrameworkCore</c> handles Shadow Property synchronization.
/// </para>
/// </remarks>
public class GranitUser : IdentityUser<Guid>, IMultiTenant, IIdentityUser, IHasMetadata
{
    private IReadOnlyDictionary<string, string>? _parsedMetadata;

    /// <summary>Gets or sets the user's first name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Gets or sets the user's last name.</summary>
    public string? LastName { get; set; }

    /// <summary>Gets or sets the tenant identifier for multi-tenant isolation.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Gets or sets the number of consecutive lockouts for exponential backoff calculation.
    /// Reset to zero on successful login, password reset, or admin unlock.
    /// </summary>
    public int ConsecutiveLockouts { get; set; }

    /// <summary>Gets or sets a value indicating whether the user has been soft-deleted.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Gets or sets the UTC timestamp when the user was soft-deleted.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Gets or sets the identifier of the user who performed the deletion.</summary>
    public string? DeletedBy { get; set; }

    /// <summary>Gets or sets a JSON column for custom extensible attributes.</summary>
    public string? CustomAttributesJson { get; set; }

    // ──── Audit fields (populated by AuditedEntityInterceptor) ────

    /// <summary>Creation timestamp (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Identifier of the user who created the entity.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Last modification timestamp (UTC).</summary>
    public DateTimeOffset? ModifiedAt { get; set; }

    /// <summary>Identifier of the user who last modified the entity.</summary>
    public string? ModifiedBy { get; set; }

    // ──── IIdentityUser (explicit implementation — zero mapping) ────

    /// <inheritdoc/>
    string IIdentityUser.UserId => Id.ToString();

    /// <inheritdoc/>
    string? IIdentityUser.Username => UserName;

    /// <inheritdoc/>
    string? IIdentityUser.Email => Email;

    /// <inheritdoc/>
#pragma warning disable GRSEC001 // Entity has no IClock access — LockoutEnd comparison needs current time
    bool IIdentityUser.Enabled => LockoutEnd is null || LockoutEnd <= DateTimeOffset.UtcNow;
#pragma warning restore GRSEC001

    /// <inheritdoc/>
    IReadOnlyDictionary<string, string> IIdentityUser.Metadata =>
        _parsedMetadata ??= DeserializeMetadata();

    // ──── IHasMetadata (explicit — maps to CustomAttributesJson) ────

    /// <inheritdoc/>
    string? IHasMetadata.MetadataJson
    {
        get => CustomAttributesJson;
        set
        {
            CustomAttributesJson = value;
            _parsedMetadata = null;
        }
    }

    // ──── Metadata helpers ────

    /// <summary>Sets an extra property. Pass <see langword="null"/> to remove.</summary>
    public void SetMetadataValue(string name, string? value)
    {
        ((IHasMetadata)this).SetMetadataValue(name, value);
        _parsedMetadata = null;
    }

    /// <summary>Gets an extra property by name.</summary>
    public string? GetMetadataValue(string name) =>
        ((IHasMetadata)this).GetMetadataValue(name);

    private ReadOnlyDictionary<string, string> DeserializeMetadata()
    {
        if (string.IsNullOrWhiteSpace(CustomAttributesJson))
        {
            return ReadOnlyDictionary<string, string>.Empty;
        }

        Dictionary<string, string>? parsed =
            JsonSerializer.Deserialize<Dictionary<string, string>>(CustomAttributesJson);

        return parsed is { Count: > 0 }
            ? new ReadOnlyDictionary<string, string>(parsed)
            : ReadOnlyDictionary<string, string>.Empty;
    }
}
