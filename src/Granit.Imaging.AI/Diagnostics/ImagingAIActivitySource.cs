using System.Diagnostics;

namespace Granit.Imaging.AI.Diagnostics;

/// <summary>OpenTelemetry activity source for AI-powered image analysis.</summary>
internal static class ImagingAIActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Imaging.AI";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
