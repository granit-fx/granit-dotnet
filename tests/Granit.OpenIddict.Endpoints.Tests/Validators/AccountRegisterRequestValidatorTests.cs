using FluentValidation.TestHelper;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Endpoints.Validators;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests.Validators;

public sealed class AccountRegisterRequestValidatorTests
{
    private readonly AccountRegisterRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        AccountRegisterRequest request = new("alice@test.com", "P@ssw0rd123", "Alice", "Doe");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_Email_Fails()
    {
        AccountRegisterRequest request = new("", "P@ssw0rd123", null, null);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Invalid_Email_Fails()
    {
        AccountRegisterRequest request = new("not-an-email", "P@ssw0rd123", null, null);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Empty_Password_Fails()
    {
        AccountRegisterRequest request = new("alice@test.com", "", null, null);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Short_Password_Fails()
    {
        AccountRegisterRequest request = new("alice@test.com", "short", null, null);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Long_FirstName_Fails()
    {
        AccountRegisterRequest request = new("alice@test.com", "P@ssw0rd123", new string('A', 257), null);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Null_Names_Passes()
    {
        AccountRegisterRequest request = new("alice@test.com", "P@ssw0rd123", null, null);
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }
}
