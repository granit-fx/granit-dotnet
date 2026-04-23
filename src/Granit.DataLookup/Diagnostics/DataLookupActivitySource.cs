using System.Diagnostics;

namespace Granit.DataLookup.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.DataLookup distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class DataLookupActivitySource
{
    /// <summary>The name of the Granit.DataLookup <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.DataLookup";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    internal const string Search = "datalookup.search";
    internal const string Resolve = "datalookup.resolve";
}
