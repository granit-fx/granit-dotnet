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

    [Fact]
    public void Value_equality_holds_across_all_components()
    {
        PostalAddress a = new("Rue de la Loi 16", "1000", "Brussels", "BE");
        PostalAddress b = new("Rue de la Loi 16", "1000", "Brussels", "BE");

        b.ShouldBe(a);
        b.GetHashCode().ShouldBe(a.GetHashCode());
    }

    [Fact]
    public void Differing_postal_code_breaks_equality()
    {
        PostalAddress a = new("Rue de la Loi 16", "1000", "Brussels", "BE");
        PostalAddress b = a with { PostalCode = "1040" };

        b.ShouldNotBe(a);
    }
}
