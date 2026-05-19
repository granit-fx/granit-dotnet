using System.Diagnostics;

namespace Granit.AI.Anthropic.Diagnostics;

/// <summary>OpenTelemetry activity source for the Anthropic AI provider.</summary>
internal static class AIAnthropicActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.AI.Anthropic";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
