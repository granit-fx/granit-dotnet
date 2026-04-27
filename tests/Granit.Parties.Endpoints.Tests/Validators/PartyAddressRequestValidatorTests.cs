using FluentValidation.TestHelper;
using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Parties.Endpoints.Tests.Validators;

public sealed class PartyAddressRequestValidatorTests
{
    private readonly PartyAddressRequestValidator _validator = new();

    private static PartyAddressRequest Valid() =>
        new(AddressKind.Billing, "Rue de la Loi 16", "Brussels", "1000", "BE");

    [Fact]
    public void Valid_Request_Passes() =>
        _validator.TestValidate(Valid()).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("BEL")]  // 3 chars
    [InlineData("B")]    // 1 char
    [InlineData("")]
    public void Invalid_Country_Length_Fails(string country) =>
        _validator.TestValidate(Valid() with { Country = country })
            .ShouldHaveValidationErrorFor(x => x.Country);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Blank_Line1_Fails(string line1) =>
        _validator.TestValidate(Valid() with { Line1 = line1 })
            .ShouldHaveValidationErrorFor(x => x.Line1);

    [Fact]
    public void Invalid_Kind_Fails() =>
        _validator.TestValidate(Valid() with { Kind = (AddressKind)99 })
            .ShouldHaveValidationErrorFor(x => x.Kind);
}
