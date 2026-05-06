using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Activities.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Granit.Activities module.
/// Meter: <c>Granit.Activities</c>.
/// </summary>
public sealed class ActivitiesMetrics
{
    /// <summary>Meter name — <c>Granit.Activities</c>.</summary>
    public const string MeterName = "Granit.Activities";

    private const string TagTenantId = "tenant_id";
    private const string TagType = "type";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _created;
    private readonly Counter<long> _completed;
    private readonly Counter<long> _cancelled;
    private readonly Counter<long> _overdueNotified;
    private readonly Counter<long> _reminderSent;

    public ActivitiesMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _created = meter.CreateCounter<long>(
            "granit.activities.activity.created",
            description: "Number of activities created (Open state).");

        _completed = meter.CreateCounter<long>(
            "granit.activities.activity.completed",
            description: "Number of activities transitioning to Done.");

        _cancelled = meter.CreateCounter<long>(
            "granit.activities.activity.cancelled",
            description: "Number of activities transitioning to Cancelled.");

        _overdueNotified = meter.CreateCounter<long>(
            "granit.activities.activity.overdue_notified",
            description: "Number of overdue notifications emitted by the background scan.");

        _reminderSent = meter.CreateCounter<long>(
            "granit.activities.activity.reminder_sent",
            description: "Number of reminder events emitted for activities due tomorrow.");
    }

    public void RecordCreated(string? tenantId, string? type) =>
        _created.Add(1, CreateTags(tenantId, type));

    public void RecordCompleted(string? tenantId, string? type) =>
        _completed.Add(1, CreateTags(tenantId, type));

    public void RecordCancelled(string? tenantId, string? type) =>
        _cancelled.Add(1, CreateTags(tenantId, type));

    public void RecordOverdueNotified(string? tenantId, string? type) =>
        _overdueNotified.Add(1, CreateTags(tenantId, type));

    public void RecordReminderSent(string? tenantId, string? type) =>
        _reminderSent.Add(1, CreateTags(tenantId, type));

    private static TagList CreateTags(string? tenantId, string? type) => new()
    {
        { TagTenantId, tenantId ?? DefaultTenant },
        { TagType, type ?? string.Empty },
    };
}
