using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;
using Granit.DataExchange.Import.Identity;
using Granit.DataExchange.Import.Mapping;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;

/// <summary>
/// Resolves entity identity using a dedicated external ID mapping table.
/// Looks up the external ID in <see cref="DataExchangeDbContext"/>, then loads the entity from the application DbContext.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The application DbContext type.</typeparam>
internal sealed class ExternalIdResolver<TEntity, TContext>(
    IDbContextFactory<DataExchangeDbContext> importContextFactory,
    IDbContextFactory<TContext> appContextFactory,
    ImportDefinition<TEntity> definition,
    ICurrentTenant currentTenant) : IRecordIdentityResolver<TEntity>
    where TEntity : class
    where TContext : DbContext
{
    /// <inheritdoc/>
    public async Task<RecordIdentity<TEntity>> ResolveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        // External ID is stored as a special property on the entity
        // The value is extracted from the mapped "ExternalId" column
        string? externalId = ExtractExternalId(entity);
        if (string.IsNullOrEmpty(externalId))
        {
            return new RecordIdentity<TEntity> { Operation = RecordOperation.Insert };
        }

        await using DataExchangeDbContext importContext = await importContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        string definitionName = definition.Name;

        ExternalIdMappingEntity? mapping = await importContext.ExternalIdMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.DefinitionName == definitionName
                     && e.ExternalId == externalId
                     && e.TenantId == tenantId,
                cancellationToken).ConfigureAwait(false);

        if (mapping is null)
        {
            return new RecordIdentity<TEntity> { Operation = RecordOperation.Insert };
        }

        await using TContext appContext = await appContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TEntity? existing = await appContext.Set<TEntity>().FindAsync([mapping.InternalId], cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return new RecordIdentity<TEntity> { Operation = RecordOperation.Insert };
        }

        return new RecordIdentity<TEntity>
        {
            Operation = RecordOperation.Update,
            ExistingEntity = existing,
        };
    }

    private static string? ExtractExternalId(TEntity entity)
    {
        // The external ID is expected to be in a property named "ExternalId" on the entity,
        // or via the first business key property if configured for external ID mode.
        System.Reflection.PropertyInfo? prop = typeof(TEntity).GetProperty("ExternalId");
        return prop?.GetValue(entity)?.ToString();
    }
}
