using System.Globalization;

namespace Granit.Domain.ValueObjects;

/// <summary>
/// A geographic coordinate in WGS 84 decimal degrees — latitude and longitude. A reusable value object for any
/// domain that records a point on Earth: address geocoding, IP geolocation, asset tracking, store locators.
/// </summary>
/// <remarks>
/// Equality is structural over <see cref="Latitude"/> and <see cref="Longitude"/>. The constructor enforces the
/// valid WGS 84 ranges so an out-of-range coordinate can never exist; callers parsing untrusted input (e.g. a
/// third-party geocoding response) should use <see cref="TryCreate"/> to get <c>null</c> instead of an exception.
/// Serialized form is the minimal <c>{ "latitude": …, "longitude": … }</c> shape.
/// </remarks>
public sealed class GeoCoordinate : ValueObject
{
    private const double MinLatitude = -90d;
    private const double MaxLatitude = 90d;
    private const double MinLongitude = -180d;
    private const double MaxLongitude = 180d;

    /// <summary>Creates a validated coordinate.</summary>
    /// <param name="latitude">Latitude in decimal degrees, in the range [-90, 90].</param>
    /// <param name="longitude">Longitude in decimal degrees, in the range [-180, 180].</param>
    /// <exception cref="ArgumentOutOfRangeException">When a component is outside its WGS 84 range.</exception>
    public GeoCoordinate(double latitude, double longitude)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(latitude, MinLatitude);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(latitude, MaxLatitude);
        ArgumentOutOfRangeException.ThrowIfLessThan(longitude, MinLongitude);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(longitude, MaxLongitude);
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>Latitude in decimal degrees, in the range [-90, 90].</summary>
    public double Latitude { get; }

    /// <summary>Longitude in decimal degrees, in the range [-180, 180].</summary>
    public double Longitude { get; }

    /// <summary><c>true</c> when both components are within their valid WGS 84 ranges.</summary>
    public static bool IsValid(double latitude, double longitude) =>
        latitude is >= MinLatitude and <= MaxLatitude && longitude is >= MinLongitude and <= MaxLongitude;

    /// <summary>
    /// Creates a coordinate, or returns <c>null</c> when either component is out of range — the allocation-free
    /// way to map untrusted input without catching an exception.
    /// </summary>
    public static GeoCoordinate? TryCreate(double latitude, double longitude) =>
        IsValid(latitude, longitude) ? new GeoCoordinate(latitude, longitude) : null;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }

    /// <summary>Human-readable form, e.g. <c>50.8503, 4.3517</c> (invariant culture).</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Latitude}, {Longitude}");
}
