using System.Diagnostics;

namespace Granit.Observability.AI.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Observability.AI distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class ObservabilityAIActivitySource
{
    /// <summary>The name of the Granit.Observability.AI <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Observability.AI";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string AnalyzeExecute = "observability-ai.analysis.execute";
}
