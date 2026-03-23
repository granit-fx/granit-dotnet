using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Vault.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the vault module.
/// Meter: <c>Granit.Vault</c>.
/// </summary>
public sealed class VaultMetrics
{
    public const string MeterName = "Granit.Vault";

    private readonly Counter<long> _operationsCompleted;
    private readonly Counter<long> _operationsErrors;
    private readonly Histogram<double> _operationDuration;
    private readonly Counter<long> _rotationsDetected;

    public VaultMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _operationsCompleted = meter.CreateCounter<long>(
            "granit.vault.operations.completed",
            description: "Number of vault operations completed successfully.");

        _operationsErrors = meter.CreateCounter<long>(
            "granit.vault.operations.errors",
            description: "Number of vault operations that failed.");

        _operationDuration = meter.CreateHistogram<double>(
            "granit.vault.operation.duration",
            unit: "s",
            description: "Duration of vault operations in seconds.");

        _rotationsDetected = meter.CreateCounter<long>(
            "granit.vault.rotations.detected",
            description: "Number of key rotations detected.");
    }

    public void RecordOperationCompleted(string? tenantId, string operation, string provider, string status) =>
        _operationsCompleted.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "operation", operation },
            { "provider", provider },
            { "status", status },
        });

    public void RecordOperationError(string? tenantId, string operation, string provider) =>
        _operationsErrors.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "operation", operation },
            { "provider", provider },
        });

    public void RecordOperationDuration(string? tenantId, string operation, string provider, TimeSpan duration) =>
        _operationDuration.Record(duration.TotalSeconds, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "operation", operation },
            { "provider", provider },
        });

    public void RecordRotationDetected(string? tenantId, string provider) =>
        _rotationsDetected.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "provider", provider },
        });
}
