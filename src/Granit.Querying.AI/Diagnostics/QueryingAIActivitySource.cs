using System.Diagnostics;

namespace Granit.Querying.AI.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Querying.AI distributed tracing.
/// </summary>
internal static class QueryingAIActivitySource
{
    /// <summary>The name of the activity source.</summary>
    internal const string Name = "Granit.Querying.AI";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string Translate = "querying.ai.translate";
}
