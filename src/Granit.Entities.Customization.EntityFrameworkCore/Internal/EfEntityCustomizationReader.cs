using Granit.Entities.Customization.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Entities.Customization.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IEntityCustomizationReader"/>. Tenant
/// filtering is applied automatically by the DbContext's standard query
/// filters — callers never need to constrain by <c>TenantId</c> directly.
/// </summary>
internal sealed class EfEntityCustomizationReader(
    IDbContextFactory<CustomizationDbContext> contextFactory) : IEntityCustomizationReader
{
    public async Task<EntityCustomization?> GetAsync(
        string entityName,
        LayoutKind layoutKind,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);
        await using CustomizationDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.EntityCustomizations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.EntityName == entityName && x.LayoutKind == layoutKind,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EntityCustomization>> GetForTenantAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        await using CustomizationDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.EntityCustomizations
            .AsNoTracking()
            .OrderBy(x => x.EntityName).ThenBy(x => x.LayoutKind)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
