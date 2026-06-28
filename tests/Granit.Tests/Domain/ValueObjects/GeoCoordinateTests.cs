using System.Text.Json;
using Granit.Domain;
using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class GeoCoordinateTests
{
    // -------------------------------------------------------------------------
    // Construction + validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(50.8503, 4.3517)]
    [InlineData(-90, -180)]
    [InlineData(90, 180)]
    [InlineData(0, 0)]
    public void Ctor_InRange_Succeeds(double latitude, double longitude)
    {
        var coordinate = new GeoCoordinate(latitude, longitude);

        coordinate.Latitude.ShouldBe(latitude);
        coordinate.Longitude.ShouldBe(longitude);
    }

    [Theory]
    [InlineData(90.1, 0)]
    [InlineData(-90.1, 0)]
    [InlineData(0, 180.1)]
    [InlineData(0, -180.1)]
    public void Ctor_OutOfRange_Throws(double latitude, double longitude) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new GeoCoordinate(latitude, longitude));

    // -------------------------------------------------------------------------
    // TryCreate — null instead of throwing, for untrusted input
    // -------------------------------------------------------------------------

    [Fact]
    public void TryCreate_InRange_ReturnsCoordinate() =>
        GeoCoordinate.TryCreate(50.8503, 4.3517).ShouldBe(new GeoCoordinate(50.8503, 4.3517));

    [Theory]
    [InlineData(91, 0)]
    [InlineData(0, 181)]
    [InlineData(double.NaN, 0)]
    public void TryCreate_OutOfRange_ReturnsNull(double latitude, double longitude) =>
        GeoCoordinate.TryCreate(latitude, longitude).ShouldBeNull();

    // -------------------------------------------------------------------------
    // Value semantics
    // -------------------------------------------------------------------------

    [Fact]
    public void Equality_IsStructural()
    {
        var a = new GeoCoordinate(50.8503, 4.3517);
        var b = new GeoCoordinate(50.8503, 4.3517);
        var c = new GeoCoordinate(50.8503, 4.3518);

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
        a.ShouldNotBe(c);
    }

    [Fact]
    public void IsValueObject() =>
        new GeoCoordinate(1, 1).ShouldBeAssignableTo<ValueObject>();

    [Fact]
    public void ToString_IsInvariantAndHumanReadable() =>
        new GeoCoordinate(50.8503, 4.3517).ToString().ShouldBe("50.8503, 4.3517");

    // -------------------------------------------------------------------------
    // Serialization — latitude/longitude only
    // -------------------------------------------------------------------------

    [Fact]
    public void Json_RoundTrips()
    {
        var original = new GeoCoordinate(50.8503, 4.3517);

        GeoCoordinate? restored = JsonSerializer.Deserialize<GeoCoordinate>(
            JsonSerializer.Serialize(original));

        restored.ShouldBe(original);
    }
}
