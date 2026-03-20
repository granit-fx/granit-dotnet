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

    private readonly Counter<long> _exportRequests;
    private readonly Counter<long> _fragmentsReceived;
    private readonly Counter<long> _deletionRequests;
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

        _exportDuration = meter.CreateHistogram<double>(
            "granit.privacy.export.duration",
            unit: "s",
            description: "Duration of personal data export in seconds.");
    }

    public void RecordExportRequested(string? tenantId) =>
        _exportRequests.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordFragmentReceived(string? tenantId, string provider) =>
        _fragmentsReceived.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "provider", provider },
        });

    public void RecordDeletionRequested(string? tenantId) =>
        _deletionRequests.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
        });

    public void RecordExportCompleted(string? tenantId, string status, TimeSpan duration) =>
        _exportDuration.Record(duration.TotalSeconds, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "status", status },
        });
}
