using System.Diagnostics;

namespace Granit.Activities.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Activities distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class ActivitiesActivitySource
{
    /// <summary>The name of the Granit.Activities <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Activities";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────────────────────────────────────────────────────
    internal const string Create = "activities.create";
    internal const string Complete = "activities.complete";
    internal const string Cancel = "activities.cancel";
    internal const string Reassign = "activities.reassign";
    internal const string Reschedule = "activities.reschedule";
    internal const string OverdueScan = "activities.overdue_scan";
    internal const string ReminderScan = "activities.reminder_scan";
}
