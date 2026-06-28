using Granit.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain.ValueObjects;

public sealed class AddressTests
{
    [Fact]
    public void Create_MinimalArgs_NormalisesCountryToUpperCase()
    {
        var addr = Address.Create(
            street1: "rue 1", city: "Brussels", postalCode: "1000", country: "be");

        addr.Country.ShouldBe("BE");
        addr.Street1.ShouldBe("rue 1");
        addr.City.ShouldBe("Brussels");
        addr.PostalCode.ShouldBe("1000");
        addr.Street2.ShouldBeNull();
        addr.State.ShouldBeNull();
    }

    [Fact]
    public void Create_AllArgs_PopulatesEveryField()
    {
        var addr = Address.Create(
            street1: "L1", city: "C", postalCode: "1000", country: "BE",
            street2: "L2", state: "BR");

        addr.Street2.ShouldBe("L2");
        addr.State.ShouldBe("BR");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankStreet1_Throws(string street1) =>
        Should.Throw<ArgumentException>(() =>
            Address.Create(street1, "City", "1000", "BE"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankCity_Throws(string city) =>
        Should.Throw<ArgumentException>(() =>
            Address.Create("L1", city, "1000", "BE"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankPostalCode_Throws(string postalCode) =>
        Should.Throw<ArgumentException>(() =>
            Address.Create("L1", "City", postalCode, "BE"));

    [Theory]
    [InlineData("BEL")]
    [InlineData("B")]
    [InlineData("BELGIUM")]
    public void Create_InvalidCountryLength_Throws(string country) =>
        Should.Throw<ArgumentException>(() =>
            Address.Create("L1", "City", "1000", country));

    [Fact]
    public void Equality_IsStructural()
    {
        var a = Address.Create("L1", "C", "1000", "BE", "L2", "St");
        var b = Address.Create("L1", "C", "1000", "BE", "L2", "St");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DiffersOnState()
    {
        var a = Address.Create("L1", "C", "1000", "BE", state: "BR");
        var b = Address.Create("L1", "C", "1000", "BE", state: "WL");

        a.ShouldNotBe(b);
    }
}
