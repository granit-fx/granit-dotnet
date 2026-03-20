using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Identity.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the identity module.
/// Meter: <c>Granit.Identity</c>.
/// </summary>
public sealed class IdentityMetrics
{
    public const string MeterName = "Granit.Identity";

    private readonly Counter<long> _operationsCompleted;
    private readonly Counter<long> _operationsErrors;
    private readonly Histogram<double> _operationDuration;

    public IdentityMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _operationsCompleted = meter.CreateCounter<long>(
            "granit.identity.operations.completed",
            description: "Number of identity operations completed.");

        _operationsErrors = meter.CreateCounter<long>(
            "granit.identity.operations.errors",
            description: "Number of identity operations that resulted in an error.");

        _operationDuration = meter.CreateHistogram<double>(
            "granit.identity.operation.duration",
            unit: "s",
            description: "Duration of identity operations in seconds.");
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
}
