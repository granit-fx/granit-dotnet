using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Tax.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the tax module.
/// Meter: <c>Granit.Tax</c>.
/// </summary>
public sealed class TaxMetrics
{
    /// <summary>The meter name used for all tax metrics.</summary>
    public const string MeterName = "Granit.Tax";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";

    private readonly Counter<long> _calculationsCompleted;
    private readonly Counter<long> _validationsCompleted;
    private readonly Counter<long> _viesRequests;

    /// <summary>Initializes tax metrics using the specified meter factory.</summary>
    public TaxMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _calculationsCompleted = meter.CreateCounter<long>(
            "granit.tax.calculation.completed",
            description: "Number of tax calculations completed.");

        _validationsCompleted = meter.CreateCounter<long>(
            "granit.tax.validation.completed",
            description: "Number of tax ID validations completed.");

        _viesRequests = meter.CreateCounter<long>(
            "granit.tax.vies.request",
            description: "Number of VIES API requests.");
    }

    /// <summary>Records a completed tax calculation.</summary>
    public void RecordCalculation(string? tenantId, string provider)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { "provider", provider },
        };
        _calculationsCompleted.Add(1, tags);
    }

    /// <summary>Records a completed tax ID validation.</summary>
    public void RecordValidation(string? tenantId, string source, bool isValid)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { "source", source },
            { "is_valid", isValid.ToString().ToLowerInvariant() },
        };
        _validationsCompleted.Add(1, tags);
    }

    /// <summary>Records a VIES API request.</summary>
    public void RecordViesRequest(string? tenantId, bool success)
    {
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { "success", success.ToString().ToLowerInvariant() },
        };
        _viesRequests.Add(1, tags);
    }
}
