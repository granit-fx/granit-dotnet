using System.Diagnostics;

namespace Granit.Scheduling.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Scheduling distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class SchedulingActivitySource
{
    /// <summary>The name of the Granit.Scheduling <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Scheduling";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string Schedule = "scheduling.schedule";
    internal const string Cancel = "scheduling.cancel";
    internal const string Reschedule = "scheduling.reschedule";
    internal const string Execute = "scheduling.execute";
    internal const string CatchUp = "scheduling.catchup";
}
