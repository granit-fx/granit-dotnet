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

    private readonly Counter<long> _heartbeatReceived;
    private readonly Counter<long> _statusChanged;
    private readonly Counter<long> _overrideSet;
    private readonly Counter<long> _notificationGated;

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
}
