using System.Diagnostics;

namespace Granit.Timeline.Diagnostics;

/// <summary>OpenTelemetry activity source for the timeline activity-stream engine.</summary>
internal static class TimelineActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Timeline";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
