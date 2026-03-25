using System.Diagnostics;

namespace Granit.QueryEngine.AI.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.QueryEngine.AI distributed tracing.
/// </summary>
internal static class QueryEngineAIActivitySource
{
    /// <summary>The name of the activity source.</summary>
    internal const string Name = "Granit.QueryEngine.AI";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string Translate = "query_engine.ai.translate";
}
