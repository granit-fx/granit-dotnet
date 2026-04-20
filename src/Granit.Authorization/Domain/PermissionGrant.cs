using Granit.Domain;

namespace Granit.Authorization.Domain;

/// <summary>
/// Represents an explicit grant of a permission to a grantee (role, user, or OIDC client)
/// scoped to a tenant (or host if <see cref="TenantId"/> is <see langword="null"/>).
/// </summary>
/// <remarks>
/// <para>
/// ABP-style multi-provider model: <see cref="ProviderName"/> identifies the grantee kind
/// (<c>"R"</c> for role, <c>"U"</c> for user, <c>"C"</c> for OIDC client) and
/// <see cref="ProviderKey"/> holds the provider-specific identifier (role name, user id,
/// client id). Extension points may introduce additional providers (organization, group,
/// geography) without schema changes.
/// </para>
/// <para>
/// Audited via <see cref="AuditedEntity"/> interceptor (CreatedAt, CreatedBy, ModifiedAt,
/// ModifiedBy). Unique index covers <c>(TenantId, ProviderName, ProviderKey, Name)</c>.
/// </para>
/// </remarks>
public sealed class PermissionGrant : AuditedEntity, IMultiTenant
{
    /// <summary>Permission name, e.g. <c>"Invoices.Delete"</c>. Max 256 characters.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Provider identifier for the grantee. Use <see cref="PermissionGrantProviderNames"/>
    /// constants: <c>"R"</c> (role), <c>"U"</c> (user), <c>"C"</c> (OIDC client). Max 8 characters.
    /// </summary>
    public string ProviderName { get; init; } = string.Empty;

    /// <summary>
    /// Provider-specific grantee key: role name for <c>"R"</c>, user id for <c>"U"</c>,
    /// OIDC <c>client_id</c> for <c>"C"</c>. Max 256 characters.
    /// </summary>
    public string ProviderKey { get; init; } = string.Empty;

    /// <summary>Tenant scope. Null means the grant applies at the host (cross-tenant) level.</summary>
    /// <remarks>Keeps <c>set</c> to satisfy <see cref="IMultiTenant"/> interceptor injection.</remarks>
    public Guid? TenantId { get; set; }
}
