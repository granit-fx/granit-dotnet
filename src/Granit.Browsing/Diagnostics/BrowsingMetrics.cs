using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Browsing.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the browsing module. Meter: <c>Granit.Browsing</c>.
/// </summary>
/// <remarks>
/// Naming follows <c>granit.browsing.{entity}.{action}</c>. Every metric carries
/// <c>tenant_id</c> (coalesced to <c>"global"</c>) and <c>engine</c> (the provider's
/// <see cref="IHeadlessBrowser.EngineName"/>).
/// </remarks>
public sealed class BrowsingMetrics
{
    /// <summary>Meter name.</summary>
    public const string MeterName = "Granit.Browsing";

    private const string TagTenantId = "tenant_id";
    private const string TagEngine = "engine";
    private const string TagOperation = "operation";
    private const string TagErrorType = "error_type";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _pagesAcquired;
    private readonly Counter<long> _pagesReleased;
    private readonly Histogram<double> _acquireDuration;
    private readonly Histogram<double> _renderDuration;
    private readonly Counter<long> _errors;
    private readonly Counter<long> _poolDrainTimeouts;

    public BrowsingMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _pagesAcquired = meter.CreateCounter<long>(
            "granit.browsing.page.acquire",
            description: "Number of browser pages acquired from the pool.");

        _pagesReleased = meter.CreateCounter<long>(
            "granit.browsing.page.release",
            description: "Number of browser pages returned to the pool.");

        _acquireDuration = meter.CreateHistogram<double>(
            "granit.browsing.pool.acquire.duration",
            unit: "s",
            description: "Time spent waiting for a free page on AcquirePageAsync.");

        _renderDuration = meter.CreateHistogram<double>(
            "granit.browsing.render.duration",
            unit: "s",
            description: "Duration of a render operation (screenshot, PDF, navigate).");

        _errors = meter.CreateCounter<long>(
            "granit.browsing.error",
            description: "Number of provider-surfaced errors (timeouts, navigation failures, capability mismatches).");

        _poolDrainTimeouts = meter.CreateCounter<long>(
            "granit.browsing.pool.drain.timeout",
            description: "Number of pool DrainAsync operations that exceeded the configured DrainTimeout (force-disposed). VULN-200.");
    }

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
    public void RecordAcquireDuration(string engine, string? tenantId, System.TimeSpan duration) =>
        _acquireDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagEngine, engine },
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    /// <summary>Records the duration of a render operation, tagged with its kind.</summary>
    public void RecordRenderDuration(string engine, string? tenantId, string operation, System.TimeSpan duration) =>
        _renderDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagEngine, engine },
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagOperation, operation },
        });

    /// <summary>Records a provider-surfaced error.</summary>
    public void RecordError(string engine, string? tenantId, string errorType) =>
        _errors.Add(1, new TagList
        {
            { TagEngine, engine },
            { TagTenantId, tenantId ?? DefaultTenant },
            { TagErrorType, errorType },
        });

    /// <summary>Records a pool drain operation that exceeded the configured <c>DrainTimeout</c>.</summary>
    public void RecordPoolDrainTimeout(string engine) =>
        _poolDrainTimeouts.Add(1, new TagList
        {
            { TagEngine, engine },
        });
}
