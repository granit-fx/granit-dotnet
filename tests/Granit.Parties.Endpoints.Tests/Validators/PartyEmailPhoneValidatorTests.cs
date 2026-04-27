using FluentValidation.TestHelper;
using Granit.Parties.Domain;
using Granit.Parties.Endpoints.Dtos;
using Granit.Parties.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Parties.Endpoints.Tests.Validators;

public sealed class PartyEmailRequestValidatorTests
{
    private readonly PartyEmailRequestValidator _validator = new();

    [Fact]
    public void Valid_Email_Passes() =>
        _validator.TestValidate(new PartyEmailRequest("billing@acme.com")).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Invalid_Address_Fails(string address) =>
        _validator.TestValidate(new PartyEmailRequest(address))
            .ShouldHaveValidationErrorFor(x => x.Address);
}

public sealed class PartyPhoneRequestValidatorTests
{
    private readonly PartyPhoneRequestValidator _validator = new();

    [Fact]
    public void Valid_Phone_Passes() =>
        _validator.TestValidate(new PartyPhoneRequest(PhoneKind.Mobile, "+3221234567"))
            .IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Blank_Number_Fails(string number) =>
        _validator.TestValidate(new PartyPhoneRequest(PhoneKind.Mobile, number))
            .ShouldHaveValidationErrorFor(x => x.Number);

    [Fact]
    public void Invalid_Kind_Fails() =>
        _validator.TestValidate(new PartyPhoneRequest((PhoneKind)99, "+1"))
            .ShouldHaveValidationErrorFor(x => x.Kind);
}

public sealed class PartyExternalMappingRequestValidatorTests
{
    private readonly PartyExternalMappingRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        _validator.TestValidate(new PartyExternalMappingRequest("stripe", "cus_1234"))
            .IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("", "x")]
    [InlineData("stripe", "")]
    public void Blank_Args_Fail(string provider, string externalId)
    {
        TestValidationResult<PartyExternalMappingRequest> result =
            _validator.TestValidate(new PartyExternalMappingRequest(provider, externalId));
        result.IsValid.ShouldBeFalse();
    }
}
