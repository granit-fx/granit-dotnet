using System.Diagnostics;
using System.Diagnostics.Metrics;
using Granit.Presence.Domain;

namespace Granit.Presence.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the presence module.
/// Meter: <c>Granit.Presence</c>.
/// </summary>
public sealed class PresenceMetrics
{
    /// <summary>Meter name registered with <see cref="IMeterFactory"/>.</summary>
    public const string MeterName = "Granit.Presence";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";
    private const string TagFrom = "from";
    private const string TagTo = "to";
    private const string TagStatus = "status";
    private const string TagHasUntil = "has_until";
    private const string TagResourceKind = "resource_kind";
    private const string TagReason = "reason";

    /// <summary>Reasons emitted by <see cref="RecordRoomLeave"/>.</summary>
    public const string ReasonExplicit = "explicit";

    /// <summary>Reasons emitted by <see cref="RecordRoomLeave"/>.</summary>
    public const string ReasonStale = "stale";

    private readonly Counter<long> _heartbeatReceived;
    private readonly Counter<long> _statusChanged;
    private readonly Counter<long> _overrideSet;
    private readonly Counter<long> _notificationGated;
    private readonly Counter<long> _roomJoin;
    private readonly Counter<long> _roomLeave;
    private readonly Histogram<int> _roomSize;
    private readonly Histogram<int> _roomMetadataBytes;

    /// <summary>Initializes the meter and all instruments.</summary>
    public PresenceMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _heartbeatReceived = meter.CreateCounter<long>(
            "granit.presence.heartbeat.received",
            description: "Number of presence heartbeats received.");

        _statusChanged = meter.CreateCounter<long>(
            "granit.presence.status.changed",
            description: "Number of effective-status transitions detected.");

        _overrideSet = meter.CreateCounter<long>(
            "granit.presence.override.set",
            description: "Number of manual override mutations.");

        _notificationGated = meter.CreateCounter<long>(
            "granit.presence.notification.gated",
            description: "Number of notification deliveries suppressed by the presence gate.");

        _roomJoin = meter.CreateCounter<long>(
            "granit.presence.room.join",
            description: "Number of join heartbeats received against a resource room.");

        _roomLeave = meter.CreateCounter<long>(
            "granit.presence.room.leave",
            description: "Number of leaves from a resource room (explicit or stale eviction).");

        _roomSize = meter.CreateHistogram<int>(
            "granit.presence.room.size",
            unit: "{participants}",
            description: "Participant count observed when a resource room is fetched or mutated.");

        _roomMetadataBytes = meter.CreateHistogram<int>(
            "granit.presence.room.metadata.bytes",
            unit: "By",
            description: "Size in bytes of metadata supplied on a resource room join.");
    }

    /// <summary>Records a single heartbeat received from a client.</summary>
    public void RecordHeartbeat(string? tenantId) =>
        _heartbeatReceived.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    /// <summary>Records an effective-status transition.</summary>
    public void RecordStatusChanged(string? tenantId, PresenceStatus from, PresenceStatus to) =>
        _statusChanged.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagFrom, from.ToString() },
            { TagTo, to.ToString() },
        });

    /// <summary>Records a manual override mutation.</summary>
    public void RecordOverrideSet(string? tenantId, ManualPresenceStatus status, bool hasUntil) =>
        _overrideSet.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagStatus, status.ToString() },
            { TagHasUntil, hasUntil ? "true" : "false" },
        });

    /// <summary>Records a notification suppressed by the presence gate.</summary>
    public void RecordNotificationGated(string? tenantId, string channelName) =>
        _notificationGated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "channel", channelName },
        });

    /// <summary>Records a successful join (or re-heartbeat) into a resource room.</summary>
    public void RecordRoomJoin(string? tenantId, string resourceKind) =>
        _roomJoin.Add(1, new TagList
        {
            { TagResourceKind, resourceKind },
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    /// <summary>
    /// Records a leave from a resource room. <paramref name="reason"/> SHOULD be one of
    /// <see cref="ReasonExplicit"/> / <see cref="ReasonStale"/>.
    /// </summary>
    public void RecordRoomLeave(string? tenantId, string resourceKind, string reason) =>
        _roomLeave.Add(1, new TagList
        {
            { TagResourceKind, resourceKind },
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagReason, reason },
        });

    /// <summary>Records the size of a resource room observed at a join / leave / get.</summary>
    public void RecordRoomSize(string? tenantId, string resourceKind, int participantCount) =>
        _roomSize.Record(participantCount, new TagList
        {
            { TagResourceKind, resourceKind },
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    /// <summary>Records the metadata payload size in bytes on a room join.</summary>
    public void RecordRoomMetadataBytes(string resourceKind, int byteCount) =>
        _roomMetadataBytes.Record(byteCount, new TagList
        {
            { TagResourceKind, resourceKind },
        });
}
