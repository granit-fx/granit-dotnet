using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Features.Diagnostics;

/// <summary>
/// Metrics for the Granit.Features module.
/// </summary>
/// <remarks>
/// Meter name: <c>Granit.Features</c>. All metric names follow the
/// <c>granit.features.{entity}.{action}</c> convention.
/// </remarks>
public sealed class FeaturesMetrics
{
    public const string MeterName = "Granit.Features";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _valuesResolved;
    private readonly Counter<long> _overridesChanged;
    private readonly Counter<long> _limitsExceeded;
    private readonly Counter<long> _limitsChecked;

    public FeaturesMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _valuesResolved = meter.CreateCounter<long>(
            "granit.features.value.resolved",
            description: "Number of feature values resolved through the cascade.");

        _overridesChanged = meter.CreateCounter<long>(
            "granit.features.override.changed",
            description: "Number of feature override mutations (set or delete).");

        _limitsChecked = meter.CreateCounter<long>(
            "granit.features.limit.checked",
            description: "Number of feature limit guard checks.");

        _limitsExceeded = meter.CreateCounter<long>(
            "granit.features.limit.exceeded",
            description: "Number of feature limit guard checks that exceeded the plan limit.");
    }

    public void RecordValueResolved(string? tenantId, string featureName, string provider) =>
        _valuesResolved.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "feature_name", featureName },
            { "value_provider", provider },
        });

    public void RecordOverrideChanged(string? tenantId, string featureName, string operation) =>
        _overridesChanged.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "feature_name", featureName },
            { "operation", operation },
        });

    public void RecordLimitChecked(string? tenantId, string featureName) =>
        _limitsChecked.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "feature_name", featureName },
        });

    public void RecordLimitExceeded(string? tenantId, string featureName) =>
        _limitsExceeded.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "feature_name", featureName },
        });
}
