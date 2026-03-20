using System.Diagnostics;

namespace Granit.Querying.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Querying.EntityFrameworkCore distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class QueryingEfCoreActivitySource
{
    /// <summary>The name of the activity source.</summary>
    internal const string Name = "Granit.Querying.EntityFrameworkCore";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string ExecuteQuery = "querying.execute";
    internal const string ExecuteGrouped = "querying.execute_grouped";
    internal const string ExecuteStream = "querying.execute_stream";
}
