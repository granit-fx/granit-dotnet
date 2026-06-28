using Granit.Modularity;

namespace Granit.Geocoding.Nominatim;

/// <summary>
/// Granit module for the opt-in OpenStreetMap Nominatim geocoding provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitGeocodingNominatim()</c>. Because it sends the address to an external
/// service, it only takes effect when explicitly registered and added to <c>Geocoding:ProviderOrder</c>.
/// </remarks>
[DependsOn(typeof(GranitGeocodingModule))]
public sealed class GranitGeocodingNominatimModule : GranitModule;
