using System.Text.Json;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Entities;
using Granit.DataExchange.Import.Mapping;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Stores;

/// <summary>
/// EF Core implementation of <see cref="IMappingReader"/> and <see cref="IMappingWriter"/>.
/// Persists column mappings per import definition and tenant in <see cref="DataExchangeDbContext"/>.
/// </summary>
internal sealed class EfMappingStore(
    IDbContextFactory<DataExchangeDbContext> contextFactory,
    IClock clock,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant,
    ICurrentUserService currentUser) : IMappingReader, IMappingWriter
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<ImportColumnMapping>> LoadAsync(
        string definitionName, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        SavedMappingEntity? entity = await context.SavedMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.DefinitionName == definitionName && e.TenantId == tenantId, cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            return [];
        }

        List<ImportColumnMapping>? mappings = JsonSerializer.Deserialize<List<ImportColumnMapping>>(entity.MappingsJson);
        return mappings?.AsReadOnly() ?? (IReadOnlyList<ImportColumnMapping>)[];
    }

    /// <inheritdoc/>
    public async Task SaveAsync(
        string definitionName,
        IReadOnlyList<ImportColumnMapping> mappings,
        CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        string json = JsonSerializer.Serialize(mappings);

        SavedMappingEntity? existing = await context.SavedMappings
            .FirstOrDefaultAsync(
                e => e.DefinitionName == definitionName && e.TenantId == tenantId, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            existing.MappingsJson = json;
            existing.SavedAt = clock.Now;
        }
        else
        {
            context.SavedMappings.Add(new SavedMappingEntity
            {
                Id = guidGenerator.Create(),
                DefinitionName = definitionName,
                TenantId = tenantId,
                MappingsJson = json,
                SavedAt = clock.Now,
                SavedBy = currentUser.UserId ?? "system",
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
