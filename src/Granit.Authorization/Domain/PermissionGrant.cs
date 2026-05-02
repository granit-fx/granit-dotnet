using Granit.Authorization.Events;
using Granit.Domain;

namespace Granit.Authorization.Domain;

/// <summary>
/// Represents an explicit grant of a permission to a grantee (role, user, or OIDC client)
/// scoped to a tenant (or host if <see cref="TenantId"/> is <see langword="null"/>).
/// </summary>
/// <remarks>
/// <para>
/// Multi-provider model: <see cref="ProviderName"/> identifies the grantee kind
/// (<c>"R"</c> for role, <c>"U"</c> for user, <c>"C"</c> for OIDC client) and
/// <see cref="ProviderKey"/> holds the provider-specific identifier (role name, user id,
/// client id). Extension points may introduce additional providers (organization, group,
/// geography) without schema changes.
/// </para>
/// <para>
/// Aggregate root — invariants are enforced by the static <see cref="Create"/> factory
/// (required fields, max lengths). External code must NOT mutate properties after creation;
/// use <see cref="MarkAsRevoked"/> before removing the entity from the <c>DbContext</c> so
/// the revocation <see cref="PermissionGrantChangedEvent"/> is collected by the
/// <c>DomainEventDispatcherInterceptor</c>.
/// </para>
/// <para>
/// Audited via <see cref="AuditedAggregateRoot"/> (CreatedAt, CreatedBy, ModifiedAt,
/// ModifiedBy). Unique index covers <c>(TenantId, ProviderName, ProviderKey, Name)</c>.
/// </para>
/// </remarks>
public sealed class PermissionGrant : AuditedAggregateRoot, IMultiTenant
{
    /// <summary>Permission name, e.g. <c>"Invoices.Delete"</c>. Max 256 characters.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Provider identifier for the grantee. Use <see cref="PermissionGrantProviderNames"/>
    /// constants: <c>"R"</c> (role), <c>"U"</c> (user), <c>"C"</c> (OIDC client). Max 8 characters.
    /// </summary>
    public string ProviderName { get; private set; } = string.Empty;

    /// <summary>
    /// Provider-specific grantee key: role name for <c>"R"</c>, the
    /// canonical <see cref="Granit.Identity.Domain.User.Id"/> stringified
    /// for <c>"U"</c> (per ADR-051 B-step 4), or OIDC <c>client_id</c> for
    /// <c>"C"</c>. Max 256 characters.
    /// </summary>
    public string ProviderKey { get; private set; } = string.Empty;

    /// <summary>Tenant scope. Null means the grant applies at the host (cross-tenant) level.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>Required by EF Core materialization — never call from application code.</summary>
    private PermissionGrant() { }

    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }

    /// <summary>
    /// Creates a new permission grant and raises a <see cref="PermissionGrantChangedEvent"/>
    /// (<c>IsGranted = true</c>) dispatched after the transaction commits.
    /// </summary>
    /// <param name="id">Aggregate identifier (typically supplied by <c>IGuidGenerator</c>).</param>
    /// <param name="permissionName">Permission name, e.g. <c>"Invoices.Delete"</c>. Max 256 chars.</param>
    /// <param name="providerName">Provider kind: <c>"R"</c>, <c>"U"</c>, or <c>"C"</c>. Max 8 chars.</param>
    /// <param name="providerKey">Provider-specific key. Max 256 chars.</param>
    /// <param name="tenantId">Tenant scope, or <see langword="null"/> for host-level grants.</param>
    public static PermissionGrant Create(
        Guid id,
        string permissionName,
        string providerName,
        string providerKey,
        Guid? tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerKey);

        if (permissionName.Length > 256)
        {
            throw new ArgumentException("Permission name exceeds 256 characters.", nameof(permissionName));
        }

        if (providerName.Length > 8)
        {
            throw new ArgumentException("Provider name exceeds 8 characters.", nameof(providerName));
        }

        if (providerKey.Length > 256)
        {
            throw new ArgumentException("Provider key exceeds 256 characters.", nameof(providerKey));
        }

        PermissionGrant grant = new()
        {
            Id = id,
            Name = permissionName,
            ProviderName = providerName,
            ProviderKey = providerKey,
            TenantId = tenantId,
        };

        grant.AddDomainEvent(new PermissionGrantChangedEvent(
            permissionName, providerName, providerKey, tenantId, IsGranted: true));

        return grant;
    }

    /// <summary>
    /// Raises a revocation <see cref="PermissionGrantChangedEvent"/> (<c>IsGranted = false</c>)
    /// dispatched after the transaction commits. Call before removing the entity from the
    /// <c>DbContext</c> so the interceptor can collect the event from the still-tracked
    /// <c>Deleted</c> entry.
    /// </summary>
    public void MarkAsRevoked()
    {
        AddDomainEvent(new PermissionGrantChangedEvent(
            Name, ProviderName, ProviderKey, TenantId, IsGranted: false));
    }
}
