using System.Diagnostics;

namespace Granit.DataExchange.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.DataExchange distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class DataExchangeActivitySource
{
    /// <summary>The name of the Granit.DataExchange <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.DataExchange";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────

    internal const string ImportExecute = "data-exchange.import.execute";
    internal const string ExportExecute = "data-exchange.export.execute";
}
