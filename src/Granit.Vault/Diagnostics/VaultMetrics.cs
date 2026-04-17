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

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenantValue = "global";

    private readonly Counter<long> _operationsCompleted;
    private readonly Counter<long> _operationsErrors;
    private readonly Histogram<double> _operationDuration;
    private readonly Counter<long> _rotationsDetected;
    private readonly Counter<long> _secretReads;
    private readonly Counter<long> _secretCacheHits;

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

        _secretReads = meter.CreateCounter<long>(
            "granit.vault.secret.read",
            description: "Number of ISecretStore.GetSecretAsync calls, tagged with provider, outcome and cached flag.");

        _secretCacheHits = meter.CreateCounter<long>(
            "granit.vault.secret.cache_hit",
            description: "Number of secret reads served from the FusionCache decorator without touching the provider.");
    }

    public void RecordOperationCompleted(string? tenantId, string operation, string provider, string status) =>
        _operationsCompleted.Add(1, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenantValue },
            { "operation", operation },
            { "provider", provider },
            { "status", status },
        });

    public void RecordOperationError(string? tenantId, string operation, string provider) =>
        _operationsErrors.Add(1, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenantValue },
            { "operation", operation },
            { "provider", provider },
        });

    public void RecordOperationDuration(string? tenantId, string operation, string provider, TimeSpan duration) =>
        _operationDuration.Record(duration.TotalSeconds, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenantValue },
            { "operation", operation },
            { "provider", provider },
        });

    public void RecordRotationDetected(string? tenantId, string provider) =>
        _rotationsDetected.Add(1, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenantValue },
            { "provider", provider },
        });

    /// <summary>
    /// Records a secret read. Always emitted at the boundary seen by callers — when a cache
    /// decorator is installed, only the decorator calls this (providers stay silent to avoid
    /// double-counting; their ActivitySource spans already capture the SDK call).
    /// </summary>
    public void RecordSecretRead(string? tenantId, string provider, string outcome, bool cached) =>
        _secretReads.Add(1, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenantValue },
            { "provider", provider },
            { "outcome", outcome },
            { "cached", cached ? "true" : "false" },
        });

    /// <summary>Records a cache-served secret read (complementary to <see cref="RecordSecretRead"/> with <c>cached=true</c>).</summary>
    public void RecordSecretCacheHit(string? tenantId, string provider) =>
        _secretCacheHits.Add(1, new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenantValue },
            { "provider", provider },
        });
}
