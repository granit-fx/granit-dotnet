using Granit.DataProtection;
using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class AddressGeocodingTests
{
    [Fact]
    public void Pending_IsTheInitialState()
    {
        AddressGeocoding.Pending.Status.ShouldBe(AddressGeocodingStatus.Pending);
        AddressGeocoding.Pending.Coordinate.ShouldBeNull();
        AddressGeocoding.Pending.GeocodedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(GeocodeMatchPrecision.Rooftop, AddressGeocodingStatus.Resolved)]
    [InlineData(GeocodeMatchPrecision.Street, AddressGeocodingStatus.Resolved)]
    [InlineData(GeocodeMatchPrecision.Locality, AddressGeocodingStatus.Approximate)]
    public void Resolved_MapsPrecisionToStatus(GeocodeMatchPrecision precision, AddressGeocodingStatus expected)
    {
        DateTimeOffset at = DateTimeOffset.UnixEpoch;

        var geocoding = AddressGeocoding.Resolved(new GeoCoordinate(50.85, 4.35), precision, at);

        geocoding.Status.ShouldBe(expected);
        geocoding.MatchPrecision.ShouldBe(precision);
        geocoding.GeocodedAt.ShouldBe(at);
        geocoding.Latitude.ShouldBe(50.85);
        geocoding.Longitude.ShouldBe(4.35);
        geocoding.Coordinate.ShouldBe(new GeoCoordinate(50.85, 4.35));
    }

    [Fact]
    public void Failed_HasNoCoordinate_ButStampsAttemptTime()
    {
        DateTimeOffset at = DateTimeOffset.UnixEpoch;

        var geocoding = AddressGeocoding.Failed(at);

        geocoding.Status.ShouldBe(AddressGeocodingStatus.Failed);
        geocoding.Coordinate.ShouldBeNull();
        geocoding.GeocodedAt.ShouldBe(at);
    }

    [Fact]
    public void AsStale_RetainsPreviousCoordinate()
    {
        var resolved = AddressGeocoding.Resolved(
            new GeoCoordinate(50.85, 4.35), GeocodeMatchPrecision.Rooftop, DateTimeOffset.UnixEpoch);

        AddressGeocoding stale = resolved.AsStale();

        stale.Status.ShouldBe(AddressGeocodingStatus.Stale);
        stale.Coordinate.ShouldBe(new GeoCoordinate(50.85, 4.35));
    }

    [Fact]
    public void AsStale_OnPending_IsNoOp() =>
        AddressGeocoding.Pending.AsStale().ShouldBeSameAs(AddressGeocoding.Pending);

    [Fact]
    public void Equality_IsStructural()
    {
        var a = AddressGeocoding.Resolved(new GeoCoordinate(1, 2), GeocodeMatchPrecision.Rooftop, DateTimeOffset.UnixEpoch);
        var b = AddressGeocoding.Resolved(new GeoCoordinate(1, 2), GeocodeMatchPrecision.Rooftop, DateTimeOffset.UnixEpoch);

        a.ShouldBe(b);
    }

    // Decision E: the coordinate / parsed components are PII. Asserting the registry resolves them
    // proves the [SensitiveData] attribute lands on the init property (not a positional-record backing
    // field, where redaction would silently no-op) and documents the framework-wide Confidential classification.
    [Theory]
    [InlineData(nameof(AddressGeocoding.Latitude))]
    [InlineData(nameof(AddressGeocoding.Longitude))]
    [InlineData(nameof(AddressGeocoding.HouseNumber))]
    [InlineData(nameof(AddressGeocoding.PoBox))]
    public void SensitiveFields_AreClassifiedConfidential(string propertyName)
    {
        SensitivePropertyRegistry registry = new([typeof(AddressGeocoding).Assembly]);

        registry.TryGet(propertyName, out SensitivePropertyEntry entry).ShouldBeTrue();
        entry.Level.ShouldBe(Sensitivity.Confidential);
    }
}
