using System.Diagnostics;

namespace Granit.QueryEngine.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.QueryEngine.EntityFrameworkCore distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class QueryEngineEfCoreActivitySource
{
    /// <summary>The name of the activity source.</summary>
    internal const string Name = "Granit.QueryEngine.EntityFrameworkCore";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string ExecuteQuery = "query_engine.execute";
    internal const string ExecuteGrouped = "query_engine.execute_grouped";
    internal const string ExecuteStream = "query_engine.execute_stream";
}
