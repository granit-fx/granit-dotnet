using Granit.DataExchange.EntityFrameworkCore.Internal.Export.Entities;
using Granit.DataExchange.Export;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export.Stores;

/// <summary>
/// EF Core implementation of <see cref="IExportPresetReader"/> and <see cref="IExportPresetWriter"/>.
/// Persists export presets per definition, preset name, and tenant in <see cref="DataExchangeDbContext"/>.
/// </summary>
internal sealed class EfExportPresetStore(
    IDbContextFactory<DataExchangeDbContext> contextFactory,
    IClock clock,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant,
    ICurrentUserService currentUser) : IExportPresetReader, IExportPresetWriter
{
    /// <inheritdoc/>
    public async Task<ExportPreset?> GetAsync(
        string definitionName, string presetName, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        ExportPresetEntity? entity = await context.ExportPresets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.DefinitionName == definitionName
                     && e.PresetName == presetName
                     && e.TenantId == tenantId, cancellationToken).ConfigureAwait(false);

        return entity is null ? null : ToPreset(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExportPreset>> ListAsync(
        string definitionName, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        List<ExportPresetEntity> entities = await context.ExportPresets
            .AsNoTracking()
            .Where(e => e.DefinitionName == definitionName && e.TenantId == tenantId)
            .OrderBy(e => e.PresetName)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.ConvertAll(ToPreset).AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task SaveAsync(ExportPreset preset, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        ExportPresetEntity? existing = await context.ExportPresets
            .FirstOrDefaultAsync(
                e => e.DefinitionName == preset.DefinitionName
                     && e.PresetName == preset.PresetName
                     && e.TenantId == tenantId, cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            existing.Fields = [.. preset.SelectedFields];
            existing.Format = preset.Format;
            existing.IncludeIdForImport = preset.IncludeIdForImport;
            existing.SavedAt = clock.Now;
        }
        else
        {
            context.ExportPresets.Add(new ExportPresetEntity
            {
                Id = guidGenerator.Create(),
                DefinitionName = preset.DefinitionName,
                PresetName = preset.PresetName,
                TenantId = tenantId,
                Fields = [.. preset.SelectedFields],
                Format = preset.Format,
                IncludeIdForImport = preset.IncludeIdForImport,
                SavedAt = clock.Now,
                SavedBy = currentUser.UserId ?? "system",
            });
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string definitionName, string presetName, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        ExportPresetEntity? entity = await context.ExportPresets
            .FirstOrDefaultAsync(
                e => e.DefinitionName == definitionName
                     && e.PresetName == presetName
                     && e.TenantId == tenantId, cancellationToken).ConfigureAwait(false);

        if (entity is not null)
        {
            context.ExportPresets.Remove(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static ExportPreset ToPreset(ExportPresetEntity entity) =>
        new(
            entity.DefinitionName,
            entity.PresetName,
            entity.Fields.AsReadOnly(),
            entity.Format,
            entity.IncludeIdForImport);
}
