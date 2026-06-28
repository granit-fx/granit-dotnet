using Granit.Modularity;

namespace Granit.Geocoding.Photon;

/// <summary>
/// Granit module for the opt-in Photon (<c>photon.komoot.io</c>) geocoding provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitGeocodingPhoton()</c>. Because it sends the address to an external
/// service, it only takes effect when explicitly registered and added to <c>Geocoding:ProviderOrder</c>.
/// </remarks>
[DependsOn(typeof(GranitGeocodingModule))]
public sealed class GranitGeocodingPhotonModule : GranitModule;
