using System.Diagnostics;

namespace Granit.Hostnames.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Hostnames distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class HostnamesActivitySource
{
    /// <summary>The name of the Granit.Hostnames <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Hostnames";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ──── Operation names ────
    internal const string Resolve = "hostnames.resolve";
}
