using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.Abstractions.Tests;

public sealed class GeoLocationTests
{
    [Fact]
    public void Every_member_defaults_to_null_so_a_country_only_source_can_populate_partially()
    {
        GeoLocation location = new() { Country = "Belgium", CountryCode = "BE" };

        location.Country.ShouldBe("Belgium");
        location.CountryCode.ShouldBe("BE");
        location.City.ShouldBeNull();
        location.Region.ShouldBeNull();
        location.Latitude.ShouldBeNull();
        location.Longitude.ShouldBeNull();
    }

    [Fact]
    public void Value_equality_holds_across_all_members()
    {
        GeoLocation a = new()
        {
            City = "Brussels",
            Region = "Brussels-Capital",
            Country = "Belgium",
            CountryCode = "BE",
            Latitude = 50.8476,
            Longitude = 4.3572,
        };
        GeoLocation b = new()
        {
            City = "Brussels",
            Region = "Brussels-Capital",
            Country = "Belgium",
            CountryCode = "BE",
            Latitude = 50.8476,
            Longitude = 4.3572,
        };

        b.ShouldBe(a);
        b.GetHashCode().ShouldBe(a.GetHashCode());
    }

    [Fact]
    public void Differing_coordinates_break_equality()
    {
        GeoLocation a = new() { City = "Brussels", Latitude = 50.85 };
        GeoLocation b = a with { Latitude = 51.0 };

        b.ShouldNotBe(a);
    }
}
