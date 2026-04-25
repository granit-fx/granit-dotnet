using System.Diagnostics;

namespace Granit.Http.Bulkhead.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Http.Bulkhead distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class BulkheadActivitySource
{
    /// <summary>The name of the Granit.Http.Bulkhead <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Http.Bulkhead";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
