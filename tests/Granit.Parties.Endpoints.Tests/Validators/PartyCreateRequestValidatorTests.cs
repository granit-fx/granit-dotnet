using FluentValidation.TestHelper;
using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Parties.Endpoints.Tests.Validators;

public sealed class PartyCreateRequestValidatorTests
{
    private readonly PartyCreateRequestValidator _validator = new();

    private static PartyCreateRequest Valid() =>
        new(PartyKind.Company, "Acme", "EUR");

    [Fact]
    public void Valid_Request_Passes() =>
        _validator.TestValidate(Valid()).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Blank_Name_Fails(string? name) =>
        _validator.TestValidate(Valid() with { Name = name! })
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Long_Name_Fails() =>
        _validator.TestValidate(Valid() with { Name = new string('x', 257) })
            .ShouldHaveValidationErrorFor(x => x.Name);

    [Theory]
    [InlineData("EU")]
    [InlineData("EURO")]
    [InlineData("")]
    public void Invalid_Currency_Length_Fails(string currency) =>
        _validator.TestValidate(Valid() with { DefaultCurrency = currency })
            .ShouldHaveValidationErrorFor(x => x.DefaultCurrency);

    [Fact]
    public void Invalid_Kind_Fails() =>
        _validator.TestValidate(Valid() with { Kind = (PartyKind)99 })
            .ShouldHaveValidationErrorFor(x => x.Kind);
}
