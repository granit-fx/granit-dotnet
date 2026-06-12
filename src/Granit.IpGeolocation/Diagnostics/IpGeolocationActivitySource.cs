using System.Diagnostics;

namespace Granit.IpGeolocation.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for <c>Granit.IpGeolocation</c> distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class IpGeolocationActivitySource
{
    /// <summary>The name of the <c>Granit.IpGeolocation</c> <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.IpGeolocation";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
