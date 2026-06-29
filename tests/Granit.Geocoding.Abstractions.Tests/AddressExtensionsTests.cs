using Granit.Domain.ValueObjects;
using Granit.Geocoding.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Geocoding.Abstractions.Tests;

public sealed class AddressExtensionsTests
{
    [Fact]
    public void ToPostalAddress_MapsDomainFieldsOntoGeocodingInput()
    {
        var address = Address.Create(
            street1: "Rue de la Loi 16",
            city: "Brussels",
            postalCode: "1000",
            country: "BE",
            street2: "Bte 2",
            state: "BRU");

        var postal = address.ToPostalAddress();

        postal.Street.ShouldBe("Rue de la Loi 16");
        postal.PostalCode.ShouldBe("1000");
        postal.Locality.ShouldBe("Brussels");
        postal.Country.ShouldBe("BE");
    }
}
