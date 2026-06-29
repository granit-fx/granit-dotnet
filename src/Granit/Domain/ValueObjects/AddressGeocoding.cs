using System.Text.Json.Serialization;
using Granit.DataProtection;

namespace Granit.Domain.ValueObjects;

/// <summary>
/// Materialized geocoding outcome for a postal address — the "can we place it on a map?" axis, independent
/// of <see cref="AddressVerification"/> (the "is it real / deliverable?" axis). A reusable owned value
/// object embedded by any address-holding entity.
/// </summary>
/// <remarks>
/// <para>
/// The portable source of truth is the <see cref="Latitude"/>/<see cref="Longitude"/> scalar pair: it
/// works on every database (SQLite included) and feeds the dashboard map widget's lat/lng column path.
/// <see cref="Coordinate"/> is a computed convenience excluded from persistence and JSON. A PostGIS
/// <c>geography(Point)</c> column, when a host opts in, is a derived spatial index over the same scalars —
/// never the source.
/// </para>
/// <para>
/// Persisted as flat columns via the framework's <c>MapAddressGeocoding</c> EF helper (not the
/// default value-object JSON path), so <see cref="Status"/> stays directly queryable for dashboards and the
/// coordinate feeds map widgets without a JSON projection. Equality is structural over all stored components.
/// </para>
/// </remarks>
public sealed class AddressGeocoding : ValueObject
{
    private AddressGeocoding() { }

    /// <summary>Initial state for a freshly attached address — awaiting a first geocoding attempt.</summary>
    public static AddressGeocoding Pending { get; } = new() { Status = AddressGeocodingStatus.Pending };

    /// <summary>
    /// Creates a resolved geocoding from a provider result. The status is
    /// <see cref="AddressGeocodingStatus.Approximate"/> when the match is only a locality centroid,
    /// otherwise <see cref="AddressGeocodingStatus.Resolved"/>.
    /// </summary>
    /// <param name="coordinate">The resolved coordinate (required).</param>
    /// <param name="precision">Granularity of the match.</param>
    /// <param name="geocodedAt">When the geocoding ran.</param>
    /// <param name="houseNumber">Optional house/building number parsed by the provider.</param>
    /// <param name="poBox">Optional PO box parsed by the provider.</param>
    public static AddressGeocoding Resolved(
        GeoCoordinate coordinate,
        GeocodeMatchPrecision precision,
        DateTimeOffset geocodedAt,
        string? houseNumber = null,
        string? poBox = null)
    {
        ArgumentNullException.ThrowIfNull(coordinate);
        return new AddressGeocoding
        {
            Latitude = coordinate.Latitude,
            Longitude = coordinate.Longitude,
            Status = precision == GeocodeMatchPrecision.Locality
                ? AddressGeocodingStatus.Approximate
                : AddressGeocodingStatus.Resolved,
            MatchPrecision = precision,
            GeocodedAt = geocodedAt,
            HouseNumber = houseNumber,
            PoBox = poBox,
        };
    }

    /// <summary>Creates a failed geocoding (no match) stamped with the attempt time.</summary>
    /// <param name="attemptedAt">When the failed attempt ran.</param>
    public static AddressGeocoding Failed(DateTimeOffset attemptedAt) =>
        new() { Status = AddressGeocodingStatus.Failed, GeocodedAt = attemptedAt };

    /// <summary>
    /// Returns a stale copy (the owning address changed since the last geocoding): the previous coordinate
    /// is retained transiently so a map stays populated until the next resolve replaces it. A
    /// <see cref="AddressGeocodingStatus.Pending"/> geocoding has nothing to retain and is returned as-is.
    /// </summary>
    public AddressGeocoding AsStale() =>
        Status == AddressGeocodingStatus.Pending
            ? this
            : new AddressGeocoding
            {
                Latitude = Latitude,
                Longitude = Longitude,
                Status = AddressGeocodingStatus.Stale,
                MatchPrecision = MatchPrecision,
                GeocodedAt = GeocodedAt,
                HouseNumber = HouseNumber,
                PoBox = PoBox,
            };

    /// <summary>Latitude in decimal degrees, or <c>null</c> until resolved. Portable source of truth.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public double? Latitude { get; init; }

    /// <summary>Longitude in decimal degrees, or <c>null</c> until resolved. Portable source of truth.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public double? Longitude { get; init; }

    /// <summary>Lifecycle state of the geocoding attempt.</summary>
    public AddressGeocodingStatus Status { get; init; } = AddressGeocodingStatus.Pending;

    /// <summary>Granularity of the resolved match, or <c>null</c> when not resolved.</summary>
    public GeocodeMatchPrecision? MatchPrecision { get; init; }

    /// <summary>When the last geocoding attempt ran, or <c>null</c> if never attempted.</summary>
    public DateTimeOffset? GeocodedAt { get; init; }

    /// <summary>House / building number parsed by the geocoding provider (axis-2 enrichment), or <c>null</c>.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? HouseNumber { get; init; }

    /// <summary>PO box parsed by the geocoding provider, or <c>null</c>.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? PoBox { get; init; }

    /// <summary>
    /// The resolved coordinate as a <see cref="GeoCoordinate"/>, or <c>null</c> when not resolved. Computed
    /// from the scalar pair — excluded from EF mapping (the flat lat/lon columns are the source) and from JSON.
    /// </summary>
    [JsonIgnore]
    public GeoCoordinate? Coordinate =>
        Latitude is { } latitude && Longitude is { } longitude
            ? GeoCoordinate.TryCreate(latitude, longitude)
            : null;

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
        yield return Status;
        yield return MatchPrecision;
        yield return GeocodedAt;
        yield return HouseNumber;
        yield return PoBox;
    }
}
