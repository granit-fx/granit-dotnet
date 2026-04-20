using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.MyModule.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the MyModule module.
/// Meter: <c>Granit.MyModule</c>.
/// </summary>
public sealed class MyModuleMetrics
{
    /// <summary>The meter name used for all MyModule metrics.</summary>
    public const string MeterName = "Granit.MyModule";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _sampleOperations;

    /// <summary>Initializes MyModule metrics using the specified meter factory.</summary>
    public MyModuleMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _sampleOperations = meter.CreateCounter<long>(
            "granit.mymodule.sample.executed",
            description: "Number of sample operations executed.");
    }

    /// <summary>Records a sample operation. Replace with real metric methods for your module.</summary>
    public void RecordSampleExecuted(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _sampleOperations.Add(1, tags);
    }
}
