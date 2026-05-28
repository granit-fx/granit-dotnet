using System.Globalization;
using System.Text.Json;
using Granit.Auditing;
using Granit.Auditing.Domain;
using Granit.Auditing.Extensions;
using Granit.Timeline.Abstractions;

namespace Granit.Timeline.Auditing.Internal;

/// <summary>
/// Projects <see cref="AuditEntry"/> rows into the federated activity stream.
/// Registered as <see cref="ITimelineSource"/> by
/// <c>AddGranitTimelineAuditing</c>; once present, every audited mutation on
/// an entity (or any of its audited children, via
/// <see cref="IAuditChildResolver"/>) shows up in that entity's timeline
/// without per-module wiring.
/// </summary>
internal sealed class AuditingTimelineSource(
    IAuditingReader auditing,
    IEnumerable<IAuditEntityTypeAliasProvider> aliasProviders,
    IEnumerable<IAuditChildResolver> childResolvers) : ITimelineSource
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

        IReadOnlyCollection<AuditChildScope> children = await childResolvers
            .ResolveAllAsync(entityType, entityId, cancellationToken)
            .ConfigureAwait(false);

        // Parent-only fast path keeps the cached GetByEntityAsync hot for
        // entities with no child-resolver contribution. The batch overload
        // intentionally skips FusionCache (per-request target sets cause
        // cache-key explosion), so a no-children call should never go through
        // it.
        if (children.Count == 0)
        {
            Granit.QueryEngine.PagedResult<AuditEntry> page = await auditing
                .GetByEntityAsync(entityType, entityId, page: 1, pageSize: limit, cancellationToken)
                .ConfigureAwait(false);
            return [.. page.Items.Select(e => Project(matchTypes, entityId, children, e))];
        }

        IReadOnlyCollection<AuditEntityRef> targets = children.ToTargets(
            new AuditEntityRef(entityType, entityId));

        IReadOnlyList<AuditEntry> entries = await auditing
            .GetByEntitiesAsync(targets, limit, cancellationToken)
            .ConfigureAwait(false);

        return [.. entries.Select(e => Project(matchTypes, entityId, children, e))];
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
        IReadOnlyCollection<AuditChildScope> children = await childResolvers
            .ResolveAllAsync(entityType, entityId, cancellationToken)
            .ConfigureAwait(false);

        // Security: refuse to anchor an audit row that does not target the
        // claimed entity OR any of its resolved children. Without the child
        // branch, a forged URL like /timeline/Page/{otherPageId}?audit=X
        // could anchor a PageVersion audit from a different aggregate.
        bool targetsEntity = entry.EntityChanges.Any(c =>
            matchTypes.Contains(c.EntityType) &&
            string.Equals(c.EntityId, entityId, StringComparison.Ordinal));

        bool targetsChild = !targetsEntity && entry.EntityChanges.Any(c =>
            children.Any(scope =>
                string.Equals(scope.ChildEntityType, c.EntityType, StringComparison.Ordinal) &&
                scope.ChildEntityIds.Contains(c.EntityId)));

        return targetsEntity || targetsChild
            ? Project(matchTypes, entityId, children, entry)
            : null;
    }

    private static TimelineStreamEntry Project(
        IReadOnlySet<string> matchTypes,
        string entityId,
        IReadOnlyCollection<AuditChildScope> children,
        AuditEntry entry)
    {
        AuditEntityChange? change = entry.EntityChanges.FirstOrDefault(c =>
            matchTypes.Contains(c.EntityType) &&
            string.Equals(c.EntityId, entityId, StringComparison.Ordinal));

        // Child-aggregated audits don't match the parent ref — fall back to the
        // child change so the body reflects what actually mutated.
        change ??= entry.EntityChanges.FirstOrDefault(c =>
            children.Any(scope =>
                string.Equals(scope.ChildEntityType, c.EntityType, StringComparison.Ordinal) &&
                scope.ChildEntityIds.Contains(c.EntityId)));

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
            entityType = change?.EntityType,
            entityId = change?.EntityId,
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
