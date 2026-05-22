using System.Globalization;
using System.Text.Json;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Auditing.Extensions;
using Granit.QueryEngine;
using Granit.Timeline.Abstractions;

namespace Granit.Timeline.Auditing.Internal;

/// <summary>
/// Projects <see cref="AuditEntry"/> rows into the federated activity stream.
/// Registered as <see cref="ITimelineSource"/> by
/// <c>AddGranitTimelineAuditing</c>; once present, every audited mutation on
/// an entity shows up in that entity's timeline without per-module wiring.
/// </summary>
internal sealed class AuditingTimelineSource(
    IAuditingReader auditing,
    IEnumerable<IAuditEntityTypeAliasProvider> aliasProviders) : ITimelineSource
{
    /// <inheritdoc/>
    public string SourceKey => "auditing";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TimelineStreamEntry>> GetEntriesAsync(
        string entityType,
        string entityId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        IReadOnlySet<string> matchTypes = aliasProviders.Resolve(entityType);

        // Single page sized to the merger's fetch budget — the reader only
        // ever asks for top-K and merges across sources.
        PagedResult<AuditEntry> result = await auditing
            .GetByEntityAsync(entityType, entityId, page: 1, pageSize: limit, cancellationToken)
            .ConfigureAwait(false);

        return [.. result.Items.Select(e => Project(matchTypes, entityId, e))];
    }

    /// <inheritdoc/>
    public async Task<TimelineStreamEntry?> GetEntryAsync(
        string entityType,
        string entityId,
        string sourceId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(sourceId, out Guid auditId))
        {
            return null;
        }

        AuditEntry? entry = await auditing.GetByIdAsync(auditId, cancellationToken).ConfigureAwait(false);
        if (entry is null)
        {
            return null;
        }

        IReadOnlySet<string> matchTypes = aliasProviders.Resolve(entityType);

        // Security: refuse to anchor an audit row that does not target this
        // entity — the caller's URL claims an entity scope that must match
        // either the canonical name or any aliased CLR name.
        bool targetsEntity = entry.EntityChanges.Any(c =>
            matchTypes.Contains(c.EntityType) &&
            string.Equals(c.EntityId, entityId, StringComparison.Ordinal));

        return targetsEntity ? Project(matchTypes, entityId, entry) : null;
    }

    private static TimelineStreamEntry Project(IReadOnlySet<string> matchTypes, string entityId, AuditEntry entry)
    {
        AuditEntityChange? change = entry.EntityChanges.FirstOrDefault(c =>
            matchTypes.Contains(c.EntityType) &&
            string.Equals(c.EntityId, entityId, StringComparison.Ordinal));

        return new TimelineStreamEntry
        {
            Id = entry.Id,
            OccurredAt = entry.Timestamp,
            EntryType = TimelineStreamEntryType.SystemLog,
            AuthorId = entry.UserId,
            AuthorName = entry.UserName ?? entry.UserId,
            Body = BuildBody(entry, change),
            Origin = TimelineEntryOrigin.External,
            SourceKey = "auditing",
            SourceId = entry.Id.ToString("D", CultureInfo.InvariantCulture),
            Attachments = [],
        };
    }

    private static string BuildBody(AuditEntry entry, AuditEntityChange? change)
    {
        // Structured JSON — system-log entries already follow this convention
        // (see TimelineEntry.Body remarks). The front renders a human-readable
        // diff; AI consumers parse the same payload.
        var payload = new
        {
            category = entry.Category.ToString(),
            changeType = change?.ChangeType.ToString(),
            changes = (change?.PropertyChanges ?? [])
                .Select(p => new
                {
                    property = p.PropertyName,
                    from = p.OriginalValue,
                    to = p.NewValue,
                })
                .ToArray(),
        };

        return JsonSerializer.Serialize(payload);
    }
}
