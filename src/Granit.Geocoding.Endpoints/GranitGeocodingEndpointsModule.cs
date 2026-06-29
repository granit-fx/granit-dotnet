using Granit.Http.ApiDocumentation;
using Granit.Modularity;

namespace Granit.Geocoding.Endpoints;

/// <summary>
/// Granit module for the geocoding HTTP endpoints (address autocomplete + reverse geocoding).
/// </summary>
/// <remarks>
/// The endpoints are mapped by the host via <c>app.MapGranitGeocoding()</c> and each is gated on the
/// corresponding capability (see <see cref="GeocodingCapabilities"/>), so an endpoint exists only when a
/// capable provider is installed.
/// </remarks>
[DependsOn(
    typeof(GranitHttpApiDocumentationModule),
    typeof(GranitGeocodingModule))]
public sealed class GranitGeocodingEndpointsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // No additional services: the geocoding services + GeocodingCapabilities come from GranitGeocodingModule.
    }
}
