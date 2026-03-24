using System.Collections.Concurrent;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Domain;
using Granit.Timing;
using Granit.Users;

namespace Granit.Timeline.Internal;

/// <summary>
/// In-memory implementation of <see cref="ITimelineWriter"/> for development and tests.
/// </summary>
internal sealed class InMemoryTimelineStore(
    IClock clock,
    ICurrentUserService currentUser,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant) : ITimelineWriter
{
    private readonly AuditContext _audit = new(guidGenerator, clock, currentUser, currentTenant);

    internal readonly ConcurrentDictionary<Guid, TimelineEntry> Entries = new();
    internal readonly ConcurrentDictionary<Guid, TimelineAttachment> Attachments = new();

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

        Entries[entry.Id] = entry;
        return Task.FromResult(entry);
    }

    /// <inheritdoc/>
    public Task DeleteEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        if (!Entries.TryGetValue(entryId, out TimelineEntry? entry))
        {
            throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");
        }

        entry.SoftDelete(clock.Now, currentUser.UserId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<TimelineAttachment> AddAttachmentAsync(
        Guid entryId,
        Guid blobId,
        string fileName,
        string contentType,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (!Entries.ContainsKey(entryId))
        {
            throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");
        }

        TimelineAttachment attachment = TimelineEntityFactory.CreateAttachment(
            entryId, blobId, fileName, contentType, sizeBytes, _audit);

        Attachments[attachment.Id] = attachment;
        return Task.FromResult(attachment);
    }
}
