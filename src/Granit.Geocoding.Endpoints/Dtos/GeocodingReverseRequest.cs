namespace Granit.Geocoding.Endpoints.Dtos;

/// <summary>Query parameters for a reverse-geocoding lookup.</summary>
/// <param name="Lat">Latitude in decimal degrees, in the range [-90, 90].</param>
/// <param name="Lon">Longitude in decimal degrees, in the range [-180, 180].</param>
public sealed record GeocodingReverseRequest(double Lat, double Lon);
