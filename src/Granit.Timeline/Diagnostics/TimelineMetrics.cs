using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Timeline.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the timeline activity-stream engine.
/// Meter: <c>Granit.Timeline</c>.
/// </summary>
/// <remarks>
/// All metrics follow the <c>granit.timeline.{entity}.{action}</c> naming convention and include
/// <c>tenant_id</c> (coalesced to <c>"global"</c>) plus a sanitised <c>entity_type</c> via
/// <see cref="TagList"/>. Instruments are created lazily so a host that never resolves the meter
/// pays nothing.
/// </remarks>
public sealed class TimelineMetrics(IMeterFactory meterFactory)
{
    /// <summary>The meter name for this module.</summary>
    public const string MeterName = "Granit.Timeline";

    private const string TagTenantId = "tenant_id";
    private const string TagEntityType = "entity_type";
    private const string TagEntryType = "entry_type";
    private const string TagSourceKey = "source_key";
    private const string DefaultTenant = "global";
    private const string Unknown = "_unknown_";
    private const int MaxTagLength = 64;

    private readonly Meter _meter = meterFactory.Create(MeterName);

    private Counter<long>? _entriesPosted;
    private Counter<long>? _entriesEdited;
    private Counter<long>? _entriesDeleted;
    private Counter<long>? _anchorsCreated;

    private Counter<long> EntriesPosted => _entriesPosted ??= _meter.CreateCounter<long>(
        "granit.timeline.entry.posted",
        description: "Number of timeline entries posted (comments, internal notes, system logs).");

    private Counter<long> EntriesEdited => _entriesEdited ??= _meter.CreateCounter<long>(
        "granit.timeline.entry.edited",
        description: "Number of timeline entry bodies edited within the edit window.");

    private Counter<long> EntriesDeleted => _entriesDeleted ??= _meter.CreateCounter<long>(
        "granit.timeline.entry.deleted",
        description: "Number of timeline entries soft-deleted.");

    private Counter<long> AnchorsCreated => _anchorsCreated ??= _meter.CreateCounter<long>(
        "granit.timeline.anchor.created",
        description: "Number of shadow rows materialised when anchoring an external timeline source entry.");

    /// <summary>Records a posted timeline entry.</summary>
    public void RecordEntryPosted(string? tenantId, string entityType, string entryType) =>
        EntriesPosted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, Sanitize(entityType) },
            { TagEntryType, Sanitize(entryType) },
        });

    /// <summary>Records an edited timeline entry body.</summary>
    public void RecordEntryEdited(string? tenantId, string entityType) =>
        EntriesEdited.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, Sanitize(entityType) },
        });

    /// <summary>Records a soft-deleted timeline entry.</summary>
    public void RecordEntryDeleted(string? tenantId, string entityType) =>
        EntriesDeleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, Sanitize(entityType) },
        });

    /// <summary>Records a newly materialised external-source anchor (shadow row).</summary>
    public void RecordAnchorCreated(string? tenantId, string entityType, string sourceKey) =>
        AnchorsCreated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagEntityType, Sanitize(entityType) },
            { TagSourceKey, Sanitize(sourceKey) },
        });

    /// <summary>
    /// Caps tag cardinality: rejects values that are empty, over-long, or carry characters outside
    /// the bounded set, so a hostile or polymorphic value cannot explode the time-series count.
    /// </summary>
    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxTagLength)
        {
            return Unknown;
        }

        return value.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-')
            ? value
            : Unknown;
    }
}
