using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timeline.Internal;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="ITimelineWriter"/> backed by PostgreSQL.
/// </summary>
/// <remarks>
/// Each operation creates and disposes its own <see cref="TimelineDbContext"/> via
/// <see cref="IDbContextFactory{TContext}"/>, making it safe for concurrent request handling.
/// </remarks>
internal sealed class EfCoreTimelineStore(
    IDbContextFactory<TimelineDbContext> dbContextFactory,
    IClock clock,
    ICurrentUserService currentUser,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant) : ITimelineWriter
{
    private readonly AuditContext _audit = new(guidGenerator, clock, currentUser, currentTenant);

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

        await using TimelineDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.TimelineEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return entry;
    }

    /// <inheritdoc/>
    public async Task DeleteEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        await using TimelineDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TimelineEntry entry = await db.TimelineEntries
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");

        entry.SoftDelete(clock.Now, currentUser.UserId);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
        await using TimelineDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        bool entryExists = await db.TimelineEntries
            .IgnoreQueryFilters()
            .AnyAsync(e => e.Id == entryId, cancellationToken).ConfigureAwait(false);

        if (!entryExists)
        {
            throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");
        }

        TimelineAttachment attachment = TimelineEntityFactory.CreateAttachment(
            entryId, blobId, fileName, contentType, sizeBytes, _audit);

        db.TimelineAttachments.Add(attachment);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return attachment;
    }
}
