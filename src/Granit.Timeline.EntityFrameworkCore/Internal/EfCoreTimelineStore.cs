using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
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
/// All reads are implicitly scoped to the current tenant via the <see cref="IMultiTenant"/>
/// query filter applied by <c>ApplyGranitConventions</c> on <see cref="TimelineDbContext"/>.
/// Each operation creates and disposes its own <see cref="TimelineDbContext"/> via
/// <see cref="IDbContextFactory{TContext}"/>, making it safe for concurrent request handling.
/// </remarks>
internal sealed class EfCoreTimelineStore(
    IDbContextFactory<TimelineDbContext> dbContextFactory,
    IClock clock,
    ICurrentUserService currentUser,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant)
    : EfStoreBase<TimelineEntry, TimelineDbContext>(dbContextFactory), ITimelineWriter
{
    private readonly AuditContext _audit = new(guidGenerator, clock, currentUser, currentTenant);

    /// <inheritdoc/>
    public Task<TimelineEntry> PostEntryAsync(
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

        return WriteAsync(
            async db =>
            {
                db.TimelineEntries.Add(entry);
                return entry;
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public Task DeleteEntryAsync(Guid entryId, CancellationToken cancellationToken = default) =>
        WriteAsync(
            async db =>
            {
                TimelineEntry entry = await db.TimelineEntries
                    .IgnoreQueryFilters([GranitFilterNames.SoftDelete])
                    .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken).ConfigureAwait(false)
                    ?? throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");

                entry.SoftDelete(clock.Now, currentUser.UserId);
            },
            cancellationToken);

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
                // VULN-209: Do not bypass soft-delete filter — reject attachments on deleted entries
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
