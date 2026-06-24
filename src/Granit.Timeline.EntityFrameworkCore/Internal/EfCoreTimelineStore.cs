using System.Diagnostics;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Diagnostics;
using Granit.Timeline.Domain;
using Granit.Timeline.Internal;
using Granit.Timeline.Options;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ITimelineWriter"/> backed by PostgreSQL.
/// </summary>
/// <remarks>
/// All reads are implicitly scoped to the current tenant via the <see cref="Granit.Domain.IMultiTenant"/>
/// query filter applied by <c>ApplyGranitConventions</c> on <see cref="TimelineDbContext"/>.
/// Each operation creates and disposes its own <see cref="TimelineDbContext"/> via
/// <see cref="IDbContextFactory{TContext}"/>, making it safe for concurrent request handling.
/// </remarks>
internal sealed class EfCoreTimelineStore(
    IDbContextFactory<TimelineDbContext> dbContextFactory,
    IClock clock,
    ICurrentUserService currentUser,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant,
    IOptions<TimelineOptions> options,
    IEnumerable<ITimelineSource>? sources = null,
    TimelineMetrics? metrics = null)
    : EfStoreBase<TimelineEntry, TimelineDbContext>(dbContextFactory, currentTenant), ITimelineWriter
{
    private readonly AuditContext _audit = new(guidGenerator, clock, currentUser, currentTenant);
    private readonly TimelineOptions _options = options.Value;
    private readonly IEnumerable<ITimelineSource> _sources = sources ?? [];
    private readonly TimelineMetrics? _metrics = metrics;

    private string? TenantTag() => _audit.CurrentTenant.IsAvailable ? _audit.CurrentTenant.Id.ToString() : null;

    /// <inheritdoc/>
    public async Task<TimelineEntry> PostEntryAsync(
        string entityType,
        string entityId,
        TimelineEntryType entryType,
        string body,
        Guid? parentEntryId = null,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = TimelineActivitySource.Source.StartActivity("Timeline.PostEntry");
        TimelineEntry entry = TimelineEntityFactory.CreateEntry(
            entityType, entityId, entryType, body, parentEntryId, _audit);
        entry.RaisePostedEvent();

        await WriteAsync(
            async db =>
            {
                db.TimelineEntries.Add(entry);
                return entry;
            },
            cancellationToken).ConfigureAwait(false);

        _metrics?.RecordEntryPosted(TenantTag(), entry.EntityType, entry.EntryType.ToString());
        return entry;
    }

    /// <inheritdoc/>
    public async Task DeleteEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        using Activity? activity = TimelineActivitySource.Source.StartActivity("Timeline.DeleteEntry");
        string entityType = await WriteAsync(
            async db =>
            {
                TimelineEntry entry = await db.TimelineEntries
                    .IgnoreQueryFilters([GranitFilterNames.SoftDelete])
                    .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken).ConfigureAwait(false)
                    ?? throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");

                entry.SoftDelete(clock.Now, currentUser.UserId);
                return entry.EntityType;
            },
            cancellationToken).ConfigureAwait(false);

        _metrics?.RecordEntryDeleted(TenantTag(), entityType);
    }

    /// <inheritdoc/>
    public async Task UpdateEntryBodyAsync(Guid entryId, string newBody, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(newBody);

        using Activity? activity = TimelineActivitySource.Source.StartActivity("Timeline.UpdateEntryBody");
        string entityType = await WriteAsync(
            async db =>
            {
                TimelineEntry entry = await db.TimelineEntries
                    .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken).ConfigureAwait(false)
                    ?? throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");

                TimelineEditGate.EnsureEditable(entry, currentUser.UserId, clock.Now, _options.EditWindow);
                entry.UpdateBody(newBody, clock.Now);
                return entry.EntityType;
            },
            cancellationToken).ConfigureAwait(false);

        _metrics?.RecordEntryEdited(TenantTag(), entityType);
    }

    /// <inheritdoc/>
    public async Task<Guid> AnchorExternalAsync(
        string entityType,
        string entityId,
        string sourceKey,
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = TimelineActivitySource.Source.StartActivity("Timeline.AnchorExternal");
        ITimelineSource source = TimelineAnchor.ResolveSource(_sources, sourceKey);

        Guid? tenantId = _audit.CurrentTenant.IsAvailable ? _audit.CurrentTenant.Id : null;
        Guid shadowId = TimelineAnchor.ComputeShadowId(tenantId, entityType, entityId, sourceKey, sourceId);

        // Fast path: shadow already exists, no need to project from source.
        bool exists = await ExistsAsync(shadowId, cancellationToken).ConfigureAwait(false);
        if (exists)
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
            await WriteAsync(
                async db =>
                {
                    db.TimelineEntries.Add(shadow);
                    return shadow.Id;
                },
                cancellationToken).ConfigureAwait(false);

            _metrics?.RecordAnchorCreated(TenantTag(), entityType, sourceKey);
        }
        catch (DbUpdateException)
        {
            // Concurrent anchor won the PK race — the deterministic Id means
            // the existing row carries the same projection, so we converge.
        }

        return shadowId;
    }

    private Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        WriteAsync(db => db.TimelineEntries.AsNoTracking().AnyAsync(e => e.Id == id, cancellationToken), cancellationToken);

    /// <inheritdoc/>
    public Task<TimelineAttachment> AddAttachmentAsync(
        Guid entryId,
        Guid blobId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default) =>
        WriteAsync(
            async db =>
            {
                // Do not bypass soft-delete filter — reject attachments on deleted entries.
                bool entryExists = await db.TimelineEntries
                    .AnyAsync(e => e.Id == entryId, cancellationToken).ConfigureAwait(false);

                if (!entryExists)
                {
                    throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");
                }

                TimelineAttachment attachment = TimelineEntityFactory.CreateAttachment(
                    entryId, blobId, fileName, contentType, sizeBytes, _audit);

                db.TimelineAttachments.Add(attachment);
                return attachment;
            },
            cancellationToken);
}
