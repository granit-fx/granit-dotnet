using Granit.DataFiltering;
using Granit.Domain;
using Granit.Events;
using Granit.Features.Diagnostics;
using Granit.Features.EntityFrameworkCore.Entities;
using Granit.Features.Events;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.Features.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IFeatureStoreReader"/> and <see cref="IFeatureStoreWriter"/>.
/// Persists tenant-level feature overrides in the <c>feature_overrides</c> table
/// with full ISO 27001 audit trail.
/// </summary>
/// <remarks>
/// <para>
/// Registered as the replacement for <c>InMemoryFeatureStore</c> when
/// <c>AddGranitFeaturesEntityFrameworkCore</c> is called.
/// Each operation creates and disposes its own <see cref="FeaturesDbContext"/> via
/// <see cref="IDbContextFactory{TContext}"/>, making it safe for concurrent request handling.
/// </para>
/// <para>
/// The <c>MultiTenant</c> global query filter is explicitly disabled in all operations
/// because this store uses explicit <c>TenantId</c> predicates. Without this, global
/// overrides (<c>TenantId = null</c>) would be invisible when a tenant context is active
/// (the automatic filter applies <c>WHERE TenantId = @currentTenantId</c>, which excludes
/// null rows).
/// </para>
/// </remarks>
internal sealed class EfCoreFeatureStore(
    IDbContextFactory<FeaturesDbContext> contextFactory,
    ILocalEventBus eventBus,
    TimeProvider timeProvider,
    ILogger<EfCoreFeatureStore> logger,
    IDataFilter? dataFilter = null)
    : EfStoreBase<TenantFeatureOverride, FeaturesDbContext>(contextFactory), IFeatureStoreReader, IFeatureStoreWriter
{
    /// <inheritdoc/>
    public async Task<string?> GetOrNullAsync(
        string featureName,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        Guid? tenantGuid = ParseTenantId(tenantId);
        using IDisposable? _ = dataFilter?.Disable<IMultiTenant>();

        TenantFeatureOverride? row = await ReadAsync(
            async db => await db.FeatureOverrides
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    o => o.TenantId == tenantGuid && o.FeatureName == featureName,
                    cancellationToken).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        return row?.Value;
    }

    /// <inheritdoc/>
    public async Task SetAsync(
        string featureName,
        string? tenantId,
        string value,
        CancellationToken cancellationToken = default)
    {
        Guid? tenantGuid = ParseTenantId(tenantId);
        using IDisposable? _ = dataFilter?.Disable<IMultiTenant>();

        // WriteAsync auto-calls SaveChangesAsync, but TOCTOU recovery needs manual
        // save control — use ReadAsync-style context access with explicit saves.
        string? oldValue = await WriteAsync<string?>(async db =>
        {
            TenantFeatureOverride? existing = await db.FeatureOverrides
                .FirstOrDefaultAsync(
                    o => o.TenantId == tenantGuid && o.FeatureName == featureName,
                    cancellationToken).ConfigureAwait(false);

            string? old = existing?.Value;

            if (existing is null)
            {
                db.FeatureOverrides.Add(new TenantFeatureOverride
                {
                    TenantId = tenantGuid,
                    FeatureName = featureName,
                    Value = value,
                });
            }
            else
            {
                existing.Value = value;
            }

            try
            {
                // SaveChangesAsync is called by WriteAsync after this delegate returns,
                // but we need to intercept DbUpdateException for TOCTOU recovery.
                // Call it explicitly here and return — WriteAsync's save will be a no-op.
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException) when (existing is null)
            {
                // TOCTOU race: concurrent insert for the same (tenant, feature) tuple hit
                // the unique index. Treat as idempotent — re-read and update instead.
                db.ChangeTracker.Clear();

                existing = await db.FeatureOverrides
                    .FirstOrDefaultAsync(
                        o => o.TenantId == tenantGuid && o.FeatureName == featureName,
                        cancellationToken).ConfigureAwait(false);

                if (existing is not null)
                {
                    old = existing.Value;
                    existing.Value = value;
                }
            }

            return old;
        }, cancellationToken).ConfigureAwait(false);

        string tenantStr = tenantGuid?.ToString() ?? "global";
        FeaturesLog.FeatureOverrideChanged(logger, featureName, tenantStr, oldValue, value);

        await eventBus.PublishAsync(
            new FeatureOverrideChangedEvent(featureName, tenantGuid, oldValue, value, timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
            new FeatureValueChangedEvent(featureName, tenantGuid),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string featureName,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        Guid? tenantGuid = ParseTenantId(tenantId);
        using IDisposable? _ = dataFilter?.Disable<IMultiTenant>();

        string? oldValue = await WriteAsync<string?>(async db =>
        {
            TenantFeatureOverride? existing = await db.FeatureOverrides
                .FirstOrDefaultAsync(
                    o => o.TenantId == tenantGuid && o.FeatureName == featureName,
                    cancellationToken).ConfigureAwait(false);

            if (existing is null)
            {
                return null;
            }

            string old = existing.Value;
            db.FeatureOverrides.Remove(existing);
            return old;
        }, cancellationToken).ConfigureAwait(false);

        if (oldValue is null)
        {
            return;
        }

        string tenantStr = tenantGuid?.ToString() ?? "global";
        FeaturesLog.FeatureOverrideChanged(logger, featureName, tenantStr, oldValue, null);

        await eventBus.PublishAsync(
            new FeatureOverrideChangedEvent(featureName, tenantGuid, oldValue, null, timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);

        await eventBus.PublishAsync(
            new FeatureValueChangedEvent(featureName, tenantGuid),
            cancellationToken).ConfigureAwait(false);
    }

    private static Guid? ParseTenantId(string? tenantId) =>
        tenantId is not null && Guid.TryParse(tenantId, out Guid parsed) ? parsed : null;
}
