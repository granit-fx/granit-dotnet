using System.Diagnostics;
using Granit.Diagnostics;

namespace Granit.AI.Chat.Diagnostics;

/// <summary>
/// OpenTelemetry activity source for Granit.AI.Chat.
/// Registered via <see cref="GranitActivitySourceRegistry"/> at module startup.
/// </summary>
internal static class AIChatActivitySource
{
    public const string Name = "Granit.AI.Chat";

    internal static readonly ActivitySource Instance = new(Name);
}
