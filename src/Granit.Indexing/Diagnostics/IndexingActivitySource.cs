using System.Diagnostics;

namespace Granit.Indexing.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for <c>Granit.Indexing</c> distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class IndexingActivitySource
{
    /// <summary>The name of the <c>Granit.Indexing</c> <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Indexing";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    internal const string Index = "indexing.index";
    internal const string Search = "indexing.search";
    internal const string Authorize = "indexing.authorize";
}
