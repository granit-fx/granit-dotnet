using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Granit.QueryEngine;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Internal;
using Granit.Timeline.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ITimelineReader"/> backed by PostgreSQL.
/// Dispatches through <see cref="TimelineContextResolver"/> so the same reader serves both
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared"/> and
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shared mode.</b> Opens the host context and reads with standard filters applied.
/// When no tenant is active (host admin), the tenant filter is bypassed.
/// </para>
/// <para>
/// <b>Segregated + tenant scope.</b> Opens the tenant-isolated context only.
/// </para>
/// <para>
/// <b>Segregated + host-admin scope.</b> Materialises across the host context plus every
/// tenant returned by <see cref="ITenantsAccessor"/> via <see cref="ICurrentTenant.Change"/>.
/// Soft-dep on <c>ITenantsAccessor</c>: the default <c>NullTenantsAccessor</c> returns an
/// empty list — single-tenant deployments still get a host-only result.
/// </para>
/// </remarks>
internal sealed class EfCoreTimelineQuery(
    TimelineContextResolver resolver,
    IDbContextFactory<TimelineHostDbContext> hostFactory,
    ICurrentTenant currentTenant,
    ITenantsAccessor tenantsAccessor,
    IEnumerable<ITimelineSource> sources,
    IOptions<TimelineOptions> options,
    ILogger<EfCoreTimelineQuery> logger,
    IDbContextFactory<TimelineTenantDbContext>? tenantFactory = null) : ITimelineReader
{
    /// <inheritdoc/>
    public Task<TimelineStreamResult> GetStreamAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryEngineDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        TimelineStreamMerger.MergeAsync(
            entityType,
            entityId,
            page,
            pageSize,
            options.Value,
            sources,
            fetchNativeTopAsync: (limit, ct) => FetchNativeTopAsync(entityType, entityId, limit, ct),
            countNativeAsync: ct => CountNativeAsync(entityType, entityId, ct),
            logger,
            cancellationToken);

    private async Task<IReadOnlyList<TimelineStreamEntry>> FetchNativeTopAsync(
        string entityType,
        string entityId,
        int limit,
        CancellationToken cancellationToken)
    {
        List<TimelineEntry> entries = [];
        List<TimelineAttachment> attachments = [];

        if (resolver.StorageMode == DualScopeStorageMode.Segregated && !currentTenant.IsAvailable)
        {
            await MaterialiseAcrossAllTenantsAsync(entityType, entityId, limit, entries, attachments, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await using ITimelineDbContext db = await resolver
                .OpenForScopeAsync(currentTenant.IsAvailable ? currentTenant.Id : null, cancellationToken)
                .ConfigureAwait(false);

            IQueryable<TimelineEntry> query = db.TimelineEntries
                .AsNoTracking()
                .Where(e => e.EntityType == entityType && e.EntityId == entityId);

            entries = await query
                .OrderByDescending(e => e.CreatedAt)
                .Take(limit)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            if (entries.Count > 0)
            {
                List<Guid> entryIds = [.. entries.Select(e => e.Id)];
                attachments = await db.TimelineAttachments
                    .AsNoTracking()
                    .Where(a => entryIds.Contains(a.EntryId))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        if (entries.Count == 0)
        {
            return [];
        }

        ILookup<Guid, TimelineAttachment> attachmentLookup = attachments.ToLookup(a => a.EntryId);

        return [..
            entries
                .OrderByDescending(e => e.CreatedAt)
                .Take(limit)
                .Select(e => new TimelineStreamEntry
                {
                    Id = e.Id,
                    OccurredAt = e.CreatedAt,
                    EntryType = (TimelineStreamEntryType)e.EntryType,
                    AuthorId = e.AuthorId,
                    AuthorName = e.AuthorName,
                    Body = e.Body,
                    ParentEntryId = e.ParentEntryId,
                    Origin = e.SourceKey is null ? TimelineEntryOrigin.Native : TimelineEntryOrigin.External,
                    SourceKey = e.SourceKey ?? TimelineSourceKeys.Native,
                    SourceId = e.SourceId,
                    EditedAt = e.EditedAt,
                    Attachments = [..
                        attachmentLookup[e.Id]
                            .Select(a => new TimelineAttachmentInfo(a.Id, a.BlobId, a.FileName, a.ContentType, a.SizeBytes))],
                })];
    }

    private async Task<int> CountNativeAsync(string entityType, string entityId, CancellationToken cancellationToken)
    {
        if (resolver.StorageMode == DualScopeStorageMode.Segregated && !currentTenant.IsAvailable)
        {
            int total = 0;
            await using (TimelineHostDbContext host = await hostFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
            {
                total += await host.TimelineEntries
                    .AsNoTracking()
                    .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                    .Where(e => e.EntityType == entityType && e.EntityId == entityId)
                    .CountAsync(cancellationToken).ConfigureAwait(false);
            }

            if (tenantFactory is null)
            {
                return total;
            }

            IReadOnlyList<(Guid Id, string Name)> tenants = await tenantsAccessor
                .GetAllAsync(cancellationToken).ConfigureAwait(false);
            foreach ((Guid id, string name) in tenants)
            {
                using (currentTenant.Change(id, name))
                await using (TimelineTenantDbContext tenantCtx =
                    await tenantFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
                {
                    total += await tenantCtx.TimelineEntries
                        .AsNoTracking()
                        .Where(e => e.EntityType == entityType && e.EntityId == entityId)
                        .CountAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            return total;
        }

        await using ITimelineDbContext db = await resolver
            .OpenForScopeAsync(currentTenant.IsAvailable ? currentTenant.Id : null, cancellationToken)
            .ConfigureAwait(false);
        return await db.TimelineEntries
            .AsNoTracking()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .CountAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task MaterialiseAcrossAllTenantsAsync(
        string entityType,
        string entityId,
        int limit,
        List<TimelineEntry> entries,
        List<TimelineAttachment> attachments,
        CancellationToken cancellationToken)
    {
        await using (TimelineHostDbContext host = await hostFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
        {
            List<TimelineEntry> hostEntries = await host.TimelineEntries
                .AsNoTracking()
                .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
                .Where(e => e.EntityType == entityType && e.EntityId == entityId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(limit)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            entries.AddRange(hostEntries);

            if (hostEntries.Count > 0)
            {
                List<Guid> hostIds = [.. hostEntries.Select(e => e.Id)];
                attachments.AddRange(await host.TimelineAttachments
                    .AsNoTracking()
                    .Where(a => hostIds.Contains(a.EntryId))
                    .ToListAsync(cancellationToken).ConfigureAwait(false));
            }
        }

        if (tenantFactory is null)
        {
            return;
        }

        IReadOnlyList<(Guid Id, string Name)> tenants = await tenantsAccessor
            .GetAllAsync(cancellationToken).ConfigureAwait(false);

        foreach ((Guid id, string name) in tenants)
        {
            using (currentTenant.Change(id, name))
            await using (TimelineTenantDbContext tenantCtx =
                await tenantFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false))
            {
                List<TimelineEntry> partial = await tenantCtx.TimelineEntries
                    .AsNoTracking()
                    .Where(e => e.EntityType == entityType && e.EntityId == entityId)
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(limit)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

                entries.AddRange(partial);

                if (partial.Count > 0)
                {
                    List<Guid> partialIds = [.. partial.Select(e => e.Id)];
                    attachments.AddRange(await tenantCtx.TimelineAttachments
                        .AsNoTracking()
                        .Where(a => partialIds.Contains(a.EntryId))
                        .ToListAsync(cancellationToken).ConfigureAwait(false));
                }
            }
        }
    }
}
