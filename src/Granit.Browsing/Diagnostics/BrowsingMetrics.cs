using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Browsing.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the browsing module. Meter: <c>Granit.Browsing</c>.
/// </summary>
/// <remarks>
/// Naming follows <c>granit.browsing.{entity}.{action}</c>. The <c>tenant_id</c> tag is
/// coalesced to <c>"global"</c> when no tenant is active; the <c>engine</c> tag carries
/// the provider's <see cref="IHeadlessBrowser.EngineName"/>.
/// </remarks>
public sealed class BrowsingMetrics(IMeterFactory meterFactory)
{
    /// <summary>Meter name.</summary>
    public const string MeterName = "Granit.Browsing";

    private const string TagTenantId = "tenant_id";
    private const string TagEngine = "engine";
    private const string TagOperation = "operation";
    private const string TagErrorType = "error_type";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _pagesAcquired = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.browsing.pages.acquired.count",
        description: "Number of browser pages acquired from the pool.");

    private readonly Counter<long> _pagesReleased = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.browsing.pages.released.count",
        description: "Number of browser pages returned to the pool.");

    private readonly Histogram<double> _acquireDuration = meterFactory.Create(MeterName).CreateHistogram<double>(
        "granit.browsing.pool.acquire.duration",
        unit: "s",
        description: "Time spent waiting for a free page on AcquirePageAsync.");

    private readonly Histogram<double> _renderDuration = meterFactory.Create(MeterName).CreateHistogram<double>(
        "granit.browsing.render.duration",
        unit: "s",
        description: "Duration of a render operation (screenshot, PDF, navigate).");

    private readonly Counter<long> _errors = meterFactory.Create(MeterName).CreateCounter<long>(
        "granit.browsing.errors.count",
        description: "Number of provider-surfaced errors (timeouts, navigation failures, capability mismatches).");

    /// <summary>Records a page acquisition.</summary>
    public void RecordPageAcquired(string engine, string? tenantId) =>
        _pagesAcquired.Add(1, new TagList
        {
            { TagEngine, engine },
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    /// <summary>Records a page release.</summary>
    public void RecordPageReleased(string engine, string? tenantId) =>
        _pagesReleased.Add(1, new TagList
        {
            { TagEngine, engine },
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    /// <summary>Records the wait duration for a page acquisition.</summary>
    public void RecordAcquireDuration(string engine, System.TimeSpan duration) =>
        _acquireDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagEngine, engine },
        });

    /// <summary>Records the duration of a render operation, tagged with its kind.</summary>
    public void RecordRenderDuration(string engine, string operation, System.TimeSpan duration) =>
        _renderDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagEngine, engine },
            { TagOperation, operation },
        });

    /// <summary>Records a provider-surfaced error.</summary>
    public void RecordError(string engine, string errorType) =>
        _errors.Add(1, new TagList
        {
            { TagEngine, engine },
            { TagErrorType, errorType },
        });
}
