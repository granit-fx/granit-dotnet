using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.IO.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the IO module.
/// Meter: <c>Granit.IO</c>.
/// </summary>
public sealed class IoMetrics
{
    /// <summary>Meter name (<c>Granit.IO</c>).</summary>
    public const string MeterName = "Granit.IO";

    private const string TagTenantId = "tenant_id";
    private const string TagCategory = "category";
    private const string TagReason = "reason";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _tempCreated;
    private readonly Counter<long> _tempDeleted;
    private readonly Histogram<long> _tempBytes;
    private readonly Counter<long> _janitorPurged;

    /// <summary>Creates a new instance of <see cref="IoMetrics"/>.</summary>
    public IoMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        Meter meter = meterFactory.Create(MeterName);

        _tempCreated = meter.CreateCounter<long>(
            "granit.io.temp.created",
            description: "Number of temp files created.");

        _tempDeleted = meter.CreateCounter<long>(
            "granit.io.temp.deleted",
            description: "Number of temp files deleted (disposed).");

        _tempBytes = meter.CreateHistogram<long>(
            "granit.io.temp.bytes",
            unit: "By",
            description: "Final size of temp files at disposal.");

        _janitorPurged = meter.CreateCounter<long>(
            "granit.io.temp.janitor.purged",
            description: "Number of temp files purged by the janitor.");
    }

    /// <summary>Records that a temp file was created.</summary>
    public void RecordCreated(string category, string? tenantId) =>
        _tempCreated.Add(1, new TagList
        {
            { TagCategory, category },
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    /// <summary>Records that a temp file was deleted (disposed).</summary>
    public void RecordDeleted(string category, string? tenantId) =>
        _tempDeleted.Add(1, new TagList
        {
            { TagCategory, category },
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    /// <summary>Records the final size of a temp file at disposal time.</summary>
    public void RecordBytes(string category, string? tenantId, long bytes) =>
        _tempBytes.Record(bytes, new TagList
        {
            { TagCategory, category },
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    /// <summary>Records a janitor-driven purge.</summary>
    public void RecordJanitorPurged(string reason) =>
        _janitorPurged.Add(1, new TagList { { TagReason, reason } });
}
