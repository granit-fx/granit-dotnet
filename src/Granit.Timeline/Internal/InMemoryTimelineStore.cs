using System.Collections.Concurrent;
using System.Diagnostics;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timeline.Abstractions;
using Granit.Timeline.Diagnostics;
using Granit.Timeline.Domain;
using Granit.Timeline.Options;
using Granit.Timing;
using Granit.Users;
using Microsoft.Extensions.Options;

namespace Granit.Timeline.Internal;

/// <summary>
/// In-memory implementation of <see cref="ITimelineWriter"/> for development and tests.
/// </summary>
internal sealed class InMemoryTimelineStore(
    IClock clock,
    ICurrentUserService currentUser,
    IGuidGenerator guidGenerator,
    ICurrentTenant currentTenant,
    IOptions<TimelineOptions> options,
    IEnumerable<ITimelineSource>? sources = null,
    TimelineMetrics? metrics = null) : ITimelineWriter
{
    private readonly AuditContext _audit = new(guidGenerator, clock, currentUser, currentTenant);
    private readonly TimelineOptions _options = options.Value;
    private readonly IEnumerable<ITimelineSource> _sources = sources ?? [];
    private readonly TimelineMetrics? _metrics = metrics;

    internal readonly ConcurrentDictionary<Guid, TimelineEntry> Entries = new();
    internal readonly ConcurrentDictionary<Guid, TimelineAttachment> Attachments = new();

    private string? TenantTag() => currentTenant.IsAvailable ? currentTenant.Id.ToString() : null;

    /// <inheritdoc/>
    public Task<TimelineEntry> PostEntryAsync(
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

        Entries[entry.Id] = entry;
        _metrics?.RecordEntryPosted(TenantTag(), entry.EntityType, entry.EntryType.ToString());
        return Task.FromResult(entry);
    }

    /// <inheritdoc/>
    public Task DeleteEntryAsync(Guid entryId, CancellationToken cancellationToken = default)
    {
        using Activity? activity = TimelineActivitySource.Source.StartActivity("Timeline.DeleteEntry");
        if (!Entries.TryGetValue(entryId, out TimelineEntry? entry))
        {
            throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");
        }

        entry.SoftDelete(clock.Now, currentUser.UserId);
        _metrics?.RecordEntryDeleted(TenantTag(), entry.EntityType);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateEntryBodyAsync(Guid entryId, string newBody, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(newBody);

        using Activity? activity = TimelineActivitySource.Source.StartActivity("Timeline.UpdateEntryBody");
        if (!Entries.TryGetValue(entryId, out TimelineEntry? entry))
        {
            throw new KeyNotFoundException($"Timeline entry '{entryId}' not found.");
        }

        TimelineEditGate.EnsureEditable(entry, currentUser.UserId, clock.Now, _options.EditWindow);
        entry.UpdateBody(newBody, clock.Now);
        _metrics?.RecordEntryEdited(TenantTag(), entry.EntityType);
        return Task.CompletedTask;
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

        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        Guid shadowId = TimelineAnchor.ComputeShadowId(tenantId, entityType, entityId, sourceKey, sourceId);

        if (Entries.ContainsKey(shadowId))
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

        // TryAdd is the equivalent of INSERT ... ON CONFLICT DO NOTHING.
        if (Entries.TryAdd(shadow.Id, shadow))
        {
            _metrics?.RecordAnchorCreated(TenantTag(), entityType, sourceKey);
        }

        return shadow.Id;
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
