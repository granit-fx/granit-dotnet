using System.Reflection;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Identity;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Identity;

/// <summary>
/// Resolves entity identity using a dedicated external ID mapping table, one query per batch
/// against <see cref="DataExchangeDbContext"/>. Never touches the application <c>DbContext</c> —
/// found mappings resolve to <see cref="RecordOperation.Update"/> keyed on the mapped internal ID
/// (a <see cref="EntityKeyKind.PrimaryKey"/>); the executor performs the actual prefetch.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TContext">The application DbContext type (kept for API symmetry with the other resolvers).</typeparam>
internal sealed class ExternalIdResolver<TEntity, TContext>(
    IDbContextFactory<DataExchangeDbContext> importContextFactory,
    ImportDefinition<TEntity> definition,
    ICurrentTenant currentTenant) : IRecordIdentityResolver<TEntity>
    where TEntity : class
    where TContext : DbContext
{
    private static readonly PropertyInfo? ExternalIdProperty = typeof(TEntity).GetProperty("ExternalId");

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RecordIdentity>> ResolveBatchAsync(
        IReadOnlyList<TEntity> batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        string?[] externalIds = [.. batch.Select(ExtractExternalId)];
        string[] nonEmptyIds = [.. externalIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal)!];

        Dictionary<string, Guid> mappedInternalIds = new(StringComparer.Ordinal);

        if (nonEmptyIds.Length > 0)
        {
            Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
            string definitionName = definition.Name;

            await using DataExchangeDbContext importContext =
                await importContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

            List<ExternalIdMappingEntity> mappings = await importContext.ExternalIdMappings
                .AsNoTracking()
                .Where(e => e.DefinitionName == definitionName
                    && e.TenantId == tenantId
                    && nonEmptyIds.Contains(e.ExternalId))
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (ExternalIdMappingEntity mapping in mappings)
            {
                mappedInternalIds[mapping.ExternalId] = mapping.InternalId;
            }
        }

        List<RecordIdentity> results = new(batch.Count);

        for (int i = 0; i < batch.Count; i++)
        {
            string? externalId = externalIds[i];

            if (string.IsNullOrEmpty(externalId))
            {
                results.Add(RecordIdentity.Insert());
                continue;
            }

            results.Add(mappedInternalIds.TryGetValue(externalId, out Guid internalId)
                ? RecordIdentity.Update(new EntityKey(internalId), EntityKeyKind.PrimaryKey)
                : RecordIdentity.Insert(externalId));
        }

        return results;
    }

    private static string? ExtractExternalId(TEntity entity) =>
        ExternalIdProperty?.GetValue(entity)?.ToString();
}
