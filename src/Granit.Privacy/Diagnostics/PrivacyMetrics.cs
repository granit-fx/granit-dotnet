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
    private readonly Counter<long> _archivesAssembled;
    private readonly Histogram<double> _exportDuration;
    private readonly Histogram<double> _archiveAssemblyDuration;
    private readonly Histogram<double> _scopeProbeDuration;
    private readonly Histogram<double> _deletionDeadlineSlip;

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

        _archivesAssembled = meter.CreateCounter<long>(
            "granit.privacy.export.archive.assembled",
            description: "Number of personal-data export archives successfully assembled.");

        _archiveAssemblyDuration = meter.CreateHistogram<double>(
            "granit.privacy.export.archive.duration",
            unit: "s",
            description: "Duration of export archive assembly in seconds.");

        _scopeProbeDuration = meter.CreateHistogram<double>(
            "granit.privacy.scope.probe.duration",
            unit: "ms",
            description: "Duration of IPrivacyDataProvider.HasDataAsync probes during scope visibility resolution.");

        _deletionDeadlineSlip = meter.CreateHistogram<double>(
            "granit.privacy.deletion.deadline_slip",
            unit: "s",
            description:
                "Seconds between a deferred deletion's scheduled deadline and its actual execution. "
                + "Non-zero values mean the daily DeletionDeadlineEnforcerJob missed the window (GDPR Art. 17 "
                + "\"without undue delay\"). Operators should alert when p95 > a tenant-specific threshold.");
    }

    /// <summary>Records an export request.</summary>
    public void RecordExportRequested(Guid? tenantId, string? regulation = null) =>
        _exportRequests.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records a fragment received from a data provider.</summary>
    public void RecordFragmentReceived(Guid? tenantId, string providerName, string? regulation = null) =>
        _fragmentsReceived.Add(1, new TagList
        {
            { TagTenantId, tenantId?.ToString() ?? DefaultTenant },
            { TagRegulation, regulation ?? DefaultRegulation },
            { "provider_name", providerName },
        });

    /// <summary>Records a deletion request.</summary>
    public void RecordDeletionRequested(Guid? tenantId, string? regulation = null) =>
        _deletionRequests.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records a deferred deletion request.</summary>
    public void RecordDeletionDeferred(Guid? tenantId, string? regulation = null) =>
        _deletionDeferred.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records a cancelled deferred deletion.</summary>
    public void RecordDeletionCancelled(Guid? tenantId, string? regulation = null) =>
        _deletionCancelled.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records an executed deletion.</summary>
    public void RecordDeletionExecuted(Guid? tenantId, string? regulation = null) =>
        _deletionExecuted.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records a deletion reminder notification sent.</summary>
    public void RecordDeletionReminderSent(Guid? tenantId, string? regulation = null) =>
        _deletionReminders.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records the duration and status of a completed export.</summary>
    public void RecordExportCompleted(Guid? tenantId, string status, TimeSpan duration, string? regulation = null) =>
        _exportDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId?.ToString() ?? DefaultTenant },
            { TagRegulation, regulation ?? DefaultRegulation },
            { "status", status },
        });

    /// <summary>Records a completed archive assembly (success or <c>SizeLimitExceeded</c>).</summary>
    public void RecordArchiveAssembled(Guid? tenantId, string status, bool isPartial, TimeSpan duration, string? regulation = null)
    {
        TagList tags = new()
        {
            { TagTenantId, tenantId?.ToString() ?? DefaultTenant },
            { TagRegulation, regulation ?? DefaultRegulation },
            { "status", status },
            { "is_partial", isPartial ? "true" : "false" },
        };
        _archivesAssembled.Add(1, tags);
        _archiveAssemblyDuration.Record(duration.TotalSeconds, tags);
    }

    /// <summary>Records an opt-out request.</summary>
    public void RecordOptOutRequested(Guid? tenantId, string? regulation = null) =>
        _optOutRequests.Add(1, CreateTags(tenantId, regulation));

    /// <summary>Records an opt-out revocation.</summary>
    public void RecordOptOutRevoked(Guid? tenantId, string? regulation = null) =>
        _optOutRevocations.Add(1, CreateTags(tenantId, regulation));

    /// <summary>
    /// Records the duration of a single <see cref="DataExport.IPrivacyDataProvider.HasDataAsync"/>
    /// probe during scope visibility resolution. Tagged by <c>provider_name</c> +
    /// <c>tenant_id</c> only — never by request id (unbounded cardinality risk).
    /// </summary>
    public void RecordScopeProbeDuration(Guid? tenantId, string providerName, TimeSpan duration) =>
        _scopeProbeDuration.Record(duration.TotalMilliseconds, new TagList
        {
            { TagTenantId, tenantId?.ToString() ?? DefaultTenant },
            { "provider_name", providerName },
        });

    /// <summary>
    /// Records the slippage between a deferred deletion's scheduled deadline and the
    /// moment the enforcement job actually executed it. A clamped-to-zero value covers
    /// early execution; positive values indicate the daily enforcer missed the deadline
    /// (job outage, scheduler backlog, persistence lag).
    /// </summary>
    public void RecordDeletionDeadlineSlip(Guid? tenantId, TimeSpan slip, string? regulation = null) =>
        _deletionDeadlineSlip.Record(
            slip.TotalSeconds < 0 ? 0 : slip.TotalSeconds,
            CreateTags(tenantId, regulation));

    private static TagList CreateTags(Guid? tenantId, string? regulation) => new()
    {
        { TagTenantId, tenantId?.ToString() ?? DefaultTenant },
        { TagRegulation, regulation ?? DefaultRegulation },
    };
}
