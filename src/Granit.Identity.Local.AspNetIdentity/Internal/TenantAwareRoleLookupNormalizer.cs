using Granit.MultiTenancy;
using Microsoft.AspNetCore.Identity;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// <see cref="ILookupNormalizer"/> that prefixes role names with the current tenant id when a
/// tenant context is active, so the same display name (e.g. <c>"Manager"</c>) can coexist across
/// tenants on a single globally-unique <c>GranitRole.NormalizedName</c> column.
/// </summary>
/// <remarks>
/// <para>
/// ASP.NET Core Identity enforces a process-wide unique index on
/// <c>AspNetRoles.NormalizedName</c>. A naive lookup normalizer (<see cref="UpperInvariantLookupNormalizer"/>)
/// collides the first time two tenants create a role with the same display name. Prefixing the
/// normalized form with <c>T_{tenantId}_</c> when <see cref="ICurrentTenant.IsAvailable"/> is
/// <see langword="true"/> restores tenant-isolated role namespaces while keeping
/// <see cref="Microsoft.AspNetCore.Identity.IdentityRole{TKey}.Name"/> as the clean display value.
/// </para>
/// <para>
/// Host-scope roles (<see cref="Granit.Authorization.Domain.RoleMetadata"/> with
/// <see cref="MultiTenancySides.Host"/> or <see cref="MultiTenancySides.Both"/>) are seeded and
/// resolved outside a tenant context, so their normalized names stay un-prefixed.
/// </para>
/// <para>
/// <see cref="ILookupNormalizer.NormalizeName"/> is invoked by ASP.NET Identity for both
/// <c>NormalizedUserName</c> and <c>NormalizedName</c> lookups — the contract has no way
/// to distinguish users from roles from inside the normalizer. The universal prefix
/// therefore also scopes <c>LocalIdentity.NormalizedUserName</c> per tenant: users created
/// inside a tenant context are invisible to host-side <c>FindByNameAsync</c> lookups and
/// vice-versa. This matches Granit's tenant-isolation intent — host and tenant admin
/// contexts never share a user-by-name lookup in practice. Email-based lookups
/// (<see cref="NormalizeEmail"/>) stay upper-invariant so cross-context login by email
/// still works.
/// </para>
/// <para>
/// Business callers should query roles through <c>IGranitRoleLookup</c>, not through
/// <c>RoleManager&lt;GranitRole&gt;.FindByNameAsync</c>: the lookup service routes via
/// <c>IRoleMetadataStore</c> (the canonical source of truth) and handles the
/// <c>Both</c>-scope fallback that <c>RoleManager</c> cannot express from inside a
/// tenant context. See ADR-023.
/// </para>
/// <para>
/// Registered as <see cref="Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped"/>
/// by <c>GranitIdentityLocalAspNetIdentityModule</c> because <see cref="ICurrentTenant"/>
/// is scoped.
/// </para>
/// </remarks>
internal sealed class TenantAwareRoleLookupNormalizer(ICurrentTenant currentTenant) : ILookupNormalizer
{
    private const string TenantPrefixMarker = "T_";

    /// <inheritdoc />
    public string? NormalizeName(string? name)
    {
        if (name is null)
        {
            return null;
        }

        string upper = name.ToUpperInvariant();

        return currentTenant.IsAvailable
            ? $"{TenantPrefixMarker}{currentTenant.Id:N}_{upper}"
            : upper;
    }

    /// <inheritdoc />
    public string? NormalizeEmail(string? email) =>
        email?.ToUpperInvariant();
}
