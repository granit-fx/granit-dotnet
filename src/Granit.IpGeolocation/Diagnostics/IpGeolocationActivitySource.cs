using System.Diagnostics;

namespace Granit.IpGeolocation.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for <c>Granit.IpGeolocation</c> distributed tracing.
/// </summary>
/// <remarks>
/// <see cref="GranitIpGeolocationModule"/> registers this source with
/// <c>GranitActivitySourceRegistry</c> at startup, so any tracing exporter wired through
/// <c>Granit.Observability</c> picks it up automatically — no per-app configuration needed.
/// </remarks>
internal static class IpGeolocationActivitySource
{
    /// <summary>The name of the <c>Granit.IpGeolocation</c> <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.IpGeolocation";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
