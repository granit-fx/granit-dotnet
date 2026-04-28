using System.Diagnostics;

namespace Granit.Analytics.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Analytics distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class AnalyticsActivitySource
{
    /// <summary>The name of the Granit.Analytics <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Analytics";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string MetricExecute = "analytics.metric.execute";
    internal const string DashboardRender = "analytics.dashboard.render";
}
