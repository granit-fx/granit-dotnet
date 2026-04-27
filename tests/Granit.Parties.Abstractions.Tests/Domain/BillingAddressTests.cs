using Granit.Parties.Domain;
using Shouldly;
using Xunit;

namespace Granit.Parties.Abstractions.Tests.Domain;

public sealed class BillingAddressTests
{
    [Fact]
    public void Create_MinimalArgs_NormalisesCountryToUpperCase()
    {
        var addr = BillingAddress.Create(
            line1: "rue 1", city: "Brussels", postalCode: "1000", country: "be");

        addr.Country.ShouldBe("BE");
        addr.Line1.ShouldBe("rue 1");
        addr.VatNumber.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_BlankLine1_Throws(string line1) =>
        Should.Throw<ArgumentException>(() =>
            BillingAddress.Create(line1, "City", "1000", "BE"));

    [Theory]
    [InlineData("BEL")]
    [InlineData("B")]
    public void Create_InvalidCountryLength_Throws(string country) =>
        Should.Throw<ArgumentException>(() =>
            BillingAddress.Create("L1", "City", "1000", country));

    [Fact]
    public void Equality_IsStructural()
    {
        var a = BillingAddress.Create(
            "L1", "C", "1000", "BE", companyName: "Acme", vatNumber: "BE0123456789");
        var b = BillingAddress.Create(
            "L1", "C", "1000", "BE", companyName: "Acme", vatNumber: "BE0123456789");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DiffersOnVatNumber()
    {
        var a = BillingAddress.Create("L1", "C", "1000", "BE", vatNumber: "BE0123456789");
        var b = BillingAddress.Create("L1", "C", "1000", "BE", vatNumber: "BE9876543210");

        a.ShouldNotBe(b);
    }
}
