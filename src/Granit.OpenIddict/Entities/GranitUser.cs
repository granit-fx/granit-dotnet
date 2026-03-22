using System.Collections.ObjectModel;
using System.Text.Json;
using Granit.Core.Domain;
using Granit.Identity;
using Microsoft.AspNetCore.Identity;

namespace Granit.OpenIddict.Entities;

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
/// </remarks>
public class GranitUser : IdentityUser<Guid>, IMultiTenant, IIdentityUser
{
    private IReadOnlyDictionary<string, string>? _parsedExtraProperties;

    /// <summary>Gets or sets the user's first name.</summary>
    public string? FirstName { get; set; }

    /// <summary>Gets or sets the user's last name.</summary>
    public string? LastName { get; set; }

    /// <summary>Gets or sets the tenant identifier for multi-tenant isolation.</summary>
    public Guid? TenantId { get; set; }

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
    IReadOnlyDictionary<string, string> IIdentityUser.ExtraProperties =>
        _parsedExtraProperties ??= DeserializeExtraProperties();

    // ──── ExtraProperties helpers ────

    /// <summary>Sets an extra property. Pass <see langword="null"/> to remove.</summary>
    public void SetExtraProperty(string name, string? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Dictionary<string, string> props = string.IsNullOrWhiteSpace(CustomAttributesJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(CustomAttributesJson) ?? [];

        if (value is null)
        {
            props.Remove(name);
        }
        else
        {
            props[name] = value;
        }

        CustomAttributesJson = props.Count > 0 ? JsonSerializer.Serialize(props) : null;
        _parsedExtraProperties = null;
    }

    /// <summary>Gets an extra property by name.</summary>
    public string? GetExtraProperty(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return ((IIdentityUser)this).ExtraProperties.GetValueOrDefault(name);
    }

    private ReadOnlyDictionary<string, string> DeserializeExtraProperties()
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
