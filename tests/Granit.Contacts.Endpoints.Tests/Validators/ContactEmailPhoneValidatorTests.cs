using FluentValidation.TestHelper;
using Granit.Contacts.Domain;
using Granit.Contacts.Endpoints.Dtos;
using Granit.Contacts.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Contacts.Endpoints.Tests.Validators;

public sealed class ContactEmailRequestValidatorTests
{
    private readonly ContactEmailRequestValidator _validator = new();

    [Fact]
    public void Valid_Email_Passes() =>
        _validator.TestValidate(new ContactEmailRequest("billing@acme.com")).IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Invalid_Address_Fails(string address) =>
        _validator.TestValidate(new ContactEmailRequest(address))
            .ShouldHaveValidationErrorFor(x => x.Address);
}

public sealed class ContactPhoneRequestValidatorTests
{
    private readonly ContactPhoneRequestValidator _validator = new();

    [Fact]
    public void Valid_Phone_Passes() =>
        _validator.TestValidate(new ContactPhoneRequest(PhoneKind.Mobile, "+3221234567"))
            .IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Blank_Number_Fails(string number) =>
        _validator.TestValidate(new ContactPhoneRequest(PhoneKind.Mobile, number))
            .ShouldHaveValidationErrorFor(x => x.Number);

    [Fact]
    public void Invalid_Kind_Fails() =>
        _validator.TestValidate(new ContactPhoneRequest((PhoneKind)99, "+1"))
            .ShouldHaveValidationErrorFor(x => x.Kind);
}

public sealed class ContactExternalMappingRequestValidatorTests
{
    private readonly ContactExternalMappingRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes() =>
        _validator.TestValidate(new ContactExternalMappingRequest("stripe", "cus_1234"))
            .IsValid.ShouldBeTrue();

    [Theory]
    [InlineData("", "x")]
    [InlineData("stripe", "")]
    public void Blank_Args_Fail(string provider, string externalId)
    {
        TestValidationResult<ContactExternalMappingRequest> result =
            _validator.TestValidate(new ContactExternalMappingRequest(provider, externalId));
        result.IsValid.ShouldBeFalse();
    }
}
