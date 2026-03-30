using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Privacy.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the privacy module.
/// Meter: <c>Granit.Privacy</c>.
/// </summary>
public sealed class PrivacyMetrics
{
    public const string MeterName = "Granit.Privacy";

    private const string TagTenantId = "tenant_id";
    private const string TagRegulation = "regulation";
    private const string DefaultTenant = "global";
    private const string DefaultRegulation = "EU_GDPR";

    private readonly Counter<long> _exportRequests;
    private readonly Counter<long> _fragmentsReceived;
    private readonly Counter<long> _deletionRequests;
    private readonly Counter<long> _deletionDeferred;
    private readonly Counter<long> _deletionCancelled;
    private readonly Counter<long> _deletionExecuted;
    private readonly Counter<long> _deletionReminders;
    private readonly Counter<long> _optOutRequests;
    private readonly Counter<long> _optOutRevocations;
    private readonly Histogram<double> _exportDuration;

    public PrivacyMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _exportRequests = meter.CreateCounter<long>(
            "granit.privacy.export.requests",
            description: "Number of personal data export requests initiated.");

        _fragmentsReceived = meter.CreateCounter<long>(
            "granit.privacy.export.fragments.received",
            description: "Number of data fragments received from providers during export.");

        _deletionRequests = meter.CreateCounter<long>(
            "granit.privacy.deletion.requests",
            description: "Number of personal data deletion requests initiated.");

        _deletionDeferred = meter.CreateCounter<long>(
            "granit.privacy.deletion.deferred",
            description: "Number of personal data deletion requests deferred with a grace period.");

        _deletionCancelled = meter.CreateCounter<long>(
            "granit.privacy.deletion.cancelled",
            description: "Number of deferred deletion requests cancelled during the grace period.");

        _deletionExecuted = meter.CreateCounter<long>(
            "granit.privacy.deletion.executed",
            description: "Number of personal data deletions executed (immediate or after grace period).");

        _deletionReminders = meter.CreateCounter<long>(
            "granit.privacy.deletion.reminders",
            description: "Number of deletion reminder notifications sent.");

        _optOutRequests = meter.CreateCounter<long>(
            "granit.privacy.optout.requests",
            description: "Number of opt-out requests (CCPA 'Do Not Sell or Share').");

        _optOutRevocations = meter.CreateCounter<long>(
            "granit.privacy.optout.revocations",
            description: "Number of opt-out revocations.");

        _exportDuration = meter.CreateHistogram<double>(
            "granit.privacy.export.duration",
            unit: "s",
            description: "Duration of personal data export in seconds.");
    }

    /// <summary>Records an export request.</summary>
    public void RecordExportRequested(string? tenantId, string? regulation = null) =>
        _exportRequests.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records a fragment received from a data provider.</summary>
    public void RecordFragmentReceived(string? tenantId, string provider, string? regulation = null) =>
        _fragmentsReceived.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagRegulation, regulation ?? DefaultRegulation },
            { "provider", provider },
        });

    /// <summary>Records a deletion request.</summary>
    public void RecordDeletionRequested(string? tenantId, string? regulation = null) =>
        _deletionRequests.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records a deferred deletion request.</summary>
    public void RecordDeletionDeferred(string? tenantId, string? regulation = null) =>
        _deletionDeferred.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records a cancelled deferred deletion.</summary>
    public void RecordDeletionCancelled(string? tenantId, string? regulation = null) =>
        _deletionCancelled.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records an executed deletion.</summary>
    public void RecordDeletionExecuted(string? tenantId, string? regulation = null) =>
        _deletionExecuted.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records a deletion reminder notification sent.</summary>
    public void RecordDeletionReminderSent(string? tenantId, string? regulation = null) =>
        _deletionReminders.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records the duration and status of a completed export.</summary>
    public void RecordExportCompleted(string? tenantId, string status, TimeSpan duration, string? regulation = null) =>
        _exportDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagRegulation, regulation ?? DefaultRegulation },
            { "status", status },
        });

    /// <summary>Records an opt-out request.</summary>
    public void RecordOptOutRequested(string? tenantId, string? regulation = null) =>
        _optOutRequests.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records an opt-out revocation.</summary>
    public void RecordOptOutRevoked(string? tenantId, string? regulation = null) =>
        _optOutRevocations.Add(1, CreateTags(tenantId, regulation));

    private static TagList CreateTags(string? tenantId, string? regulation) => new()
    {
        { TagTenantId, tenantId ?? DefaultTenant },
        { TagRegulation, regulation ?? DefaultRegulation },
    };
}
