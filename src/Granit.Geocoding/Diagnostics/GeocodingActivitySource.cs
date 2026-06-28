using System.Diagnostics;

namespace Granit.Geocoding.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for <c>Granit.Geocoding</c> distributed tracing.
/// </summary>
/// <remarks>
/// <see cref="GranitGeocodingModule"/> registers this source with <c>GranitActivitySourceRegistry</c> at startup, so
/// any tracing exporter wired through <c>Granit.Observability</c> picks it up automatically — no per-app
/// configuration needed. Spans carry only coarse, non-identifying tags; the address value is never recorded (GDPR).
/// </remarks>
internal static class GeocodingActivitySource
{
    /// <summary>The name of the <c>Granit.Geocoding</c> <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Geocoding";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
