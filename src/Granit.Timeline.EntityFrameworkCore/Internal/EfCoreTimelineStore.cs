using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Internal;
using Granit.Timeline.Options;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ITimelineWriter"/> backed by PostgreSQL. Dispatches
/// every write through <see cref="TimelineContextResolver"/> so the same store serves both
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared"/> and
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/>
/// deployments.
/// </summary>
/// <remarks>
/// <para>
/// Writes scoped by <c>TenantId</c> at write time (<c>PostEntryAsync</c>, <c>AnchorExternalAsync</c>)
/// route through <see cref="TimelineContextResolver.OpenForScopeAsync"/> directly. Writes
/// keyed by an entry id only (<c>DeleteEntryAsync</c>, <c>UpdateEntryBodyAsync</c>,
/// <c>AddAttachmentAsync</c>) fan out via
/// <see cref="TimelineContextResolver.OpenForUnknownScopeAsync"/> and search each candidate
/// context for the target entry, then mutate where it lives.
/// </para>
/// </remarks>
internal sealed class EfCoreTimelineStore(
    TimelineContextResolver resolver,
    IClock clock,
    ICurrentUserService currentUser,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant,
    IOptions<TimelineOptions> options,
    IEnumerable<ITimelineSource>? sources = null) : ITimelineWriter
{
    private readonly AuditContext _audit = new(guidGenerator, clock, currentUser, currentTenant);
    private readonly TimelineOptions _options = options.Value;
    private readonly IEnumerable<ITimelineSource> _sources = sources ?? [];

    /// <inheritdoc/>
    public async Task<TimelineEntry> PostEntryAsync(
        string entityType,
        string entityId,
        TimelineEntryType entryType,
        string body,
        Guid? parentEntryId = null,
        CancellationToken cancellationToken = default)
    {
        TimelineEntry entry = TimelineEntityFactory.CreateEntry(
            entityType, entityId, entryType, body, parentEntryId, _audit);
        entry.RaisePostedEvent();

        await using ITimelineDbContext db = await resolver
            .OpenForScopeAsync(entry.TenantId, cancellationToken).ConfigureAwait(false);
        db.TimelineEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return entry;
    }

    /// <inheritdoc/>
    public async Task DeleteEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        await MutateEntryByIdAsync(entryId, entry => entry.SoftDelete(clock.Now, currentUser.UserId), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateEntryBodyAsync(Guid entryId, string newBody, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(newBody);

        await MutateEntryByIdAsync(
            entryId,
            entry =>
            {
                TimelineEditGate.EnsureEditable(entry, currentUser.UserId, clock.Now, _options.EditWindow);
                entry.UpdateBody(newBody, clock.Now);
            },
            cancellationToken,
            includeSoftDeleted: false).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Guid> AnchorExternalAsync(
        string entityType,
        string entityId,
        string sourceKey,
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        ITimelineSource source = TimelineAnchor.ResolveSource(_sources, sourceKey);

        Guid? tenantId = _audit.CurrentTenant.IsAvailable ? _audit.CurrentTenant.Id : null;
        Guid shadowId = TimelineAnchor.ComputeShadowId(tenantId, entityType, entityId, sourceKey, sourceId);

        if (await ExistsAsync(shadowId, tenantId, cancellationToken).ConfigureAwait(false))
        {
            return shadowId;
        }

        TimelineStreamEntry projection = await source
            .GetEntryAsync(entityType, entityId, sourceId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException(
                $"Source '{sourceKey}' has no entry for ({entityType}, {entityId}, {sourceId}).");

        TimelineEntry shadow = TimelineEntityFactory.CreateShadow(
            entityType, entityId, sourceKey, sourceId, projection, _audit);

        try
        {
            await using ITimelineDbContext db = await resolver
                .OpenForScopeAsync(shadow.TenantId, cancellationToken).ConfigureAwait(false);
            db.TimelineEntries.Add(shadow);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Concurrent anchor won the PK race — the deterministic Id means the existing
            // row carries the same projection, so we converge.
        }

        return shadowId;
    }

    /// <inheritdoc/>
    public async Task<TimelineAttachment> AddAttachmentAsync(
        Guid entryId,
        Guid blobId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ITimelineDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (ITimelineDbContext db in contexts)
            {
                // Do not bypass soft-delete filter — reject attachments on deleted entries.
                bool entryExists = await db.TimelineEntries
                    .AnyAsync(e => e.Id == entryId, cancellationToken).ConfigureAwait(false);

                if (!entryExists)
                {
                    continue;
                }

                TimelineAttachment attachment = TimelineEntityFactory.CreateAttachment(
                    entryId, blobId, fileName, contentType, sizeBytes, _audit);

                db.TimelineAttachments.Add(attachment);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return attachment;
            }

            throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    private async Task<bool> ExistsAsync(Guid id, Guid? tenantId, CancellationToken cancellationToken)
    {
        await using ITimelineDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return await db.TimelineEntries.AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private async Task MutateEntryByIdAsync(
        Guid entryId,
        Action<TimelineEntry> mutate,
        CancellationToken cancellationToken,
        bool includeSoftDeleted = true)
    {
        IReadOnlyList<ITimelineDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (ITimelineDbContext db in contexts)
            {
                IQueryable<TimelineEntry> query = db.TimelineEntries;
                if (includeSoftDeleted)
                {
                    query = query.IgnoreQueryFilters([GranitFilterNames.SoftDelete]);
                }

                TimelineEntry? entry = await query
                    .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken).ConfigureAwait(false);
                if (entry is null)
                {
                    continue;
                }

                mutate(entry);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    private static async Task DisposeAllAsync(IReadOnlyList<ITimelineDbContext> contexts)
    {
        foreach (ITimelineDbContext db in contexts)
        {
            await db.DisposeAsync().ConfigureAwait(false);
        }
    }
}
