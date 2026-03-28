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
    private const string TagFeatureName = "feature_name";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _limitsExceeded;

    public FeaturesMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _limitsExceeded = meter.CreateCounter<long>(
            "granit.features.limit.exceeded",
            description: "Number of feature limit guard checks that exceeded the plan limit.");
    }

    public void RecordLimitExceeded(string? tenantId, string featureName) =>
        _limitsExceeded.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagFeatureName, featureName },
        });
}
