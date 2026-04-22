using Granit.Authorization.Domain;

namespace Granit.Identity.Local.Services;

/// <summary>
/// Canonical role lookup for Granit-internal flows. Encapsulates the precedence
/// between tenant-scoped and Both-scope <see cref="RoleMetadata"/> rows so callers
/// do not have to replicate the fallback logic at every site.
/// </summary>
/// <remarks>
/// <para>
/// Prefer this abstraction over <c>RoleManager&lt;GranitRole&gt;.FindByNameAsync</c>
/// for business lookups. The <see cref="Microsoft.AspNetCore.Identity.RoleManager{T}"/>
/// is tied to ASP.NET Identity's <c>NormalizedName</c> indexing strategy and therefore
/// sensitive to the registered <see cref="Microsoft.AspNetCore.Identity.ILookupNormalizer"/>;
/// <see cref="IGranitRoleLookup"/> routes through <see cref="Granit.Authorization.IRoleMetadataStore"/>
/// which is the source of truth for the role catalog.
/// </para>
/// <para>
/// Resolution order when a tenant context is active:
/// <list type="number">
///   <item>A tenant-scope row matching the current tenant (<c>Side = Tenant</c>, <c>TenantId = currentTenant.Id</c>).</item>
///   <item>A host / Both scope row (<c>TenantId = null</c>) — covers the <c>Both</c> roles assignable inside tenants.</item>
/// </list>
/// Host context skips step 1. Other-tenant rows are never returned.
/// </para>
/// </remarks>
public interface IGranitRoleLookup
{
    /// <summary>
    /// Finds a role by display name, applying the tenant-aware precedence.
    /// Returns <see langword="null"/> if no matching <see cref="RoleMetadata"/>
    /// row exists in the caller's scope.
    /// </summary>
    /// <param name="name">Display name (e.g. <c>"Manager"</c>).</param>
    /// <param name="clientId">Optional OIDC client scope (reserved for Phase 2 realm/client-role distinction).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<RoleMetadata?> FindByNameAsync(
        string name,
        string? clientId = null,
        CancellationToken cancellationToken = default);
}
