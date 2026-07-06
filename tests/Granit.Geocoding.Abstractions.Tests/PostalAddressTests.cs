using Shouldly;
using Xunit;

namespace Granit.Geocoding.Abstractions.Tests;

public sealed class PostalAddressTests
{
    [Fact]
    public void Optional_components_may_be_null_while_locality_and_country_are_required()
    {
        PostalAddress address = new(Street: null, PostalCode: null, Locality: "Brussels", Country: "BE");

        address.Street.ShouldBeNull();
        address.PostalCode.ShouldBeNull();
        address.Locality.ShouldBe("Brussels");
        address.Country.ShouldBe("BE");
    }
}
