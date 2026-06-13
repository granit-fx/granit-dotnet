using System.Diagnostics;

namespace Granit.AI.Tools.Diagnostics;

/// <summary>
/// OpenTelemetry activity source for the Granit AI tools orchestration loop.
/// </summary>
internal static class AIToolsActivitySource
{
    public const string Name = "Granit.AI.Tools";

    internal static readonly ActivitySource Instance = new(Name);
}
