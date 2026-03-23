using System.Diagnostics;

namespace Granit.Timeline.AI.Diagnostics;

/// <summary>OpenTelemetry activity source for AI-powered timeline analysis.</summary>
internal static class TimelineAIActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Timeline.AI";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
