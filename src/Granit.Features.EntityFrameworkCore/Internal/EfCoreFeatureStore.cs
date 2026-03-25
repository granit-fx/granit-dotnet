using Granit.Events;
using Granit.Features.Events;
using Microsoft.EntityFrameworkCore;

namespace Granit.Features.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IFeatureStoreReader"/> and <see cref="IFeatureStoreWriter"/>.
/// Persists tenant-level feature overrides in the <c>feature_overrides</c> table
/// with full ISO 27001 audit trail.
/// </summary>
/// <remarks>
/// Registered as the replacement for <c>InMemoryFeatureStore</c> when
/// <c>AddGranitFeaturesEntityFrameworkCore</c> is called.
/// Each operation creates and disposes its own <see cref="FeaturesDbContext"/> via
/// <see cref="IDbContextFactory{TContext}"/>, making it safe for concurrent request handling.
/// </remarks>
internal sealed class EfCoreFeatureStore(
    IDbContextFactory<FeaturesDbContext> contextFactory,
    ILocalEventBus eventBus,
    TimeProvider timeProvider) : IFeatureStoreReader, IFeatureStoreWriter
{
    /// <inheritdoc/>
    public async Task<string?> GetOrNullAsync(
        string featureName,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        Guid? tenantGuid = ParseTenantId(tenantId);
        await using FeaturesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        TenantFeatureOverride? row = await context.FeatureOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.TenantId == tenantGuid && o.FeatureName == featureName,
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
        await using FeaturesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        TenantFeatureOverride? existing = await context.FeatureOverrides
            .FirstOrDefaultAsync(
                o => o.TenantId == tenantGuid && o.FeatureName == featureName,
                cancellationToken).ConfigureAwait(false);

        string? oldValue = existing?.Value;

        if (existing is null)
        {
            context.FeatureOverrides.Add(new TenantFeatureOverride
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

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
        await using FeaturesDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        TenantFeatureOverride? existing = await context.FeatureOverrides
            .FirstOrDefaultAsync(
                o => o.TenantId == tenantGuid && o.FeatureName == featureName,
                cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return;
        }

        string oldValue = existing.Value;
        context.FeatureOverrides.Remove(existing);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
