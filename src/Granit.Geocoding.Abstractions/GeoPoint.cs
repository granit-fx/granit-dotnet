namespace Granit.Geocoding;

/// <summary>
/// A geographic coordinate (WGS 84 decimal degrees) produced by geocoding a <see cref="PostalAddress"/>.
/// </summary>
/// <param name="Latitude">Latitude in decimal degrees, in the range [-90, 90].</param>
/// <param name="Longitude">Longitude in decimal degrees, in the range [-180, 180].</param>
public sealed record GeoPoint(double Latitude, double Longitude);
