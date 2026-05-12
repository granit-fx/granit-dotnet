using Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;
using Granit.Documents.PublicLinks.Options;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Documents.PublicLinks.BackgroundJobs.Jobs;

/// <summary>
/// Handles <see cref="PruneExpiredPublicLinksJob"/> — deletes
/// <c>DocumentPublicLink</c> rows whose <c>ExpiresAt</c> (or <c>RevokedAt</c>) is
/// older than the configured retention buffer.
/// </summary>
/// <remarks>
/// <para>
/// Multi-tenant query filter is bypassed: the scan runs in host context, the
/// cross-tenant <c>tenant_expires</c> index serves the predicate, and the row's
/// own <c>TenantId</c> is irrelevant to the deletion criterion.
/// </para>
/// <para>
/// <c>ExecuteDeleteAsync</c> is used so the operation is set-based: no entities
/// are materialised, no domain events are raised, and no audit interceptors fire
/// — pruning is operational housekeeping, not a business-side mutation.
/// </para>
/// </remarks>
public static partial class PruneExpiredPublicLinksHandler
{
    /// <summary>Wolverine-discovered handler entry point.</summary>
    /// <remarks>
    /// The <c>DocumentsPublicLinksDbContext</c> factory is resolved through the
    /// <see cref="IServiceProvider"/> rather than taken as a method parameter — the
    /// DbContext type is <c>internal</c> to keep the EF Core layer encapsulated,
    /// and exposing it as a method signature here would leak that type publicly.
    /// </remarks>
    public static async Task HandleAsync(
        PruneExpiredPublicLinksJob job,
        IServiceProvider serviceProvider,
        IClock clock,
        IOptionsMonitor<GranitDocumentsPublicLinksOptions> optionsMonitor,
        ILogger<PruneExpiredPublicLinksJob> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(optionsMonitor);
        ArgumentNullException.ThrowIfNull(logger);

        IDbContextFactory<DocumentsPublicLinksDbContext> contextFactory =
            serviceProvider.GetRequiredService<IDbContextFactory<DocumentsPublicLinksDbContext>>();

        PruningOptions pruning = optionsMonitor.CurrentValue.Pruning;
        if (!pruning.Enabled)
        {
            LogSkipped(logger);
            return;
        }

        DateTimeOffset now = clock.Now;
        DateTimeOffset cutoff = now - pruning.RetentionAfterRevocation;

        await using DocumentsPublicLinksDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Bypass the multi-tenant filter: housekeeping runs in host context and
        // the retention rule applies uniformly across every tenant.
        // Two-step delete (collect ids -> bulk delete by id) keeps the predicate
        // SQLite-translatable; production providers (PostgreSQL) translate the
        // direct ExecuteDelete too but the cheap id-lookup is portable.
        List<Guid> doomedIds = await context.DocumentPublicLinks
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(l => l.ExpiresAt < cutoff
                || (l.RevokedAt != null && l.RevokedAt < cutoff))
            .Select(l => l.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (doomedIds.Count == 0)
        {
            return;
        }

        int deleted = await context.DocumentPublicLinks
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(l => doomedIds.Contains(l.Id))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deleted > 0)
        {
            LogPruned(logger, deleted, cutoff);
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Pruned {DeletedCount} expired/revoked public-link row(s) older than {Cutoff:O}")]
    private static partial void LogPruned(ILogger logger, int deletedCount, DateTimeOffset cutoff);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Public-link pruning is disabled (GranitDocumentsPublicLinksOptions.Pruning.Enabled = false); skipping run.")]
    private static partial void LogSkipped(ILogger logger);
}
