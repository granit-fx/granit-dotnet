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
/// <see cref="MultiTenancySide.Host"/> or <see cref="MultiTenancySide.Both"/>) are seeded and
/// resolved outside a tenant context, so their normalized names stay un-prefixed.
/// </para>
/// <para>
/// User name normalization falls through to the standard upper-invariant behavior because
/// <c>GranitUser</c> already scopes users per tenant via <c>TenantId</c> — only the role
/// table needs the prefix strategy.
/// </para>
/// <para>
/// This implementation is intentionally not registered by
/// <c>GranitIdentityLocalAspNetIdentityModule</c>. Applications that enable
/// <c>RoleEndpointsOptions.AllowTenantRoles</c> must replace the framework default
/// <see cref="ILookupNormalizer"/> with this type (scoped lifetime required because
/// <see cref="ICurrentTenant"/> is scoped). See the Granit docs for the full wiring
/// example.
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
