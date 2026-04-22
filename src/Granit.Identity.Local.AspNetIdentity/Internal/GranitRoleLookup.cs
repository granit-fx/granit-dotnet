using Granit.Authorization;
using Granit.Authorization.Domain;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <inheritdoc cref="IGranitRoleLookup" />
internal sealed class GranitRoleLookup(
    IRoleMetadataStore roleMetadataStore,
    ICurrentTenant currentTenant) : IGranitRoleLookup
{
    /// <inheritdoc />
    public async Task<RoleMetadata?> FindByNameAsync(
        string name,
        string? clientId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (currentTenant.IsAvailable)
        {
            RoleMetadata? tenantScopedRole = await roleMetadataStore
                .FindByNameAsync(name, currentTenant.Id, clientId, cancellationToken)
                .ConfigureAwait(false);

            if (tenantScopedRole is not null)
            {
                return tenantScopedRole;
            }
        }

        return await roleMetadataStore
            .FindByNameAsync(name, tenantId: null, clientId, cancellationToken)
            .ConfigureAwait(false);
    }
}
