using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// Central dispatch helper that hands every Timeline store the right
/// <see cref="ITimelineDbContext"/> for a given scope, independently of the configured
/// <see cref="DualScopeStorageMode"/>.
/// </summary>
internal sealed class TimelineContextResolver(
    DualScopeStorageMode storageMode,
    IDbContextFactory<TimelineHostDbContext> hostFactory,
    IDbContextFactory<TimelineTenantDbContext>? tenantFactory = null)
{
    /// <summary>The active storage mode declared at registration.</summary>
    public DualScopeStorageMode StorageMode { get; } = storageMode;

    /// <summary>
    /// Opens the appropriate context for an operation whose <c>TenantId</c> is known
    /// up-front (e.g. an incoming write whose scope is set by the caller).
    /// </summary>
    public async Task<ITimelineDbContext> OpenForScopeAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return StorageMode switch
        {
            DualScopeStorageMode.Shared
                => await hostFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false),
            DualScopeStorageMode.Segregated when tenantId is null
                => await hostFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false),
            DualScopeStorageMode.Segregated
                => await RequireTenantFactory().CreateDbContextAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unknown DualScopeStorageMode: {StorageMode}."),
        };
    }

    /// <summary>
    /// Opens every context that may hold entries visible to the current scope. Under
    /// <see cref="DualScopeStorageMode.Shared"/> returns a single context; under
    /// <see cref="DualScopeStorageMode.Segregated"/> returns both the host and the active
    /// tenant context. Caller disposes every returned context.
    /// </summary>
    public async Task<IReadOnlyList<ITimelineDbContext>> OpenAllAsync(
        CancellationToken cancellationToken = default)
    {
        ITimelineDbContext host = await hostFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        if (StorageMode == DualScopeStorageMode.Shared)
        {
            return [host];
        }

        ITimelineDbContext tenant = await RequireTenantFactory()
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return [host, tenant];
    }

    /// <summary>
    /// Opens every context that might hold an entry whose scope is unknown at call time
    /// (writes by id). Same behaviour as <see cref="OpenAllAsync"/>.
    /// </summary>
    public Task<IReadOnlyList<ITimelineDbContext>> OpenForUnknownScopeAsync(
        CancellationToken cancellationToken = default)
        => OpenAllAsync(cancellationToken);

    private IDbContextFactory<TimelineTenantDbContext> RequireTenantFactory()
    {
        if (tenantFactory is null)
        {
            throw new InvalidOperationException(
                "TimelineContextResolver: storage mode is Segregated but no IDbContextFactory<TimelineTenantDbContext> " +
                "is registered. This is a bug in AddGranitTimelineEntityFrameworkCore.");
        }

        return tenantFactory;
    }
}
