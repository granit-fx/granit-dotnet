using FluentValidation.TestHelper;
using Granit.OpenIddict.Endpoints.Dtos;
using Granit.OpenIddict.Endpoints.Validators;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests.Validators;

public sealed class AccountDeleteRequestValidatorTests
{
    private readonly AccountDeleteRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        AccountDeleteRequest request = new("MySecureP@ss123");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_Password_Fails()
    {
        AccountDeleteRequest request = new("");
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Null_Password_Fails()
    {
        AccountDeleteRequest request = new(null!);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Password);
    }
}

public sealed class AccountProfileUpdateRequestValidatorTests
{
    private readonly AccountProfileUpdateRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        AccountProfileUpdateRequest request = new("Alice", "Doe");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Null_Names_Passes()
    {
        AccountProfileUpdateRequest request = new(null, null);
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Long_FirstName_Fails()
    {
        AccountProfileUpdateRequest request = new(new string('A', 257), "Doe");
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Long_LastName_Fails()
    {
        AccountProfileUpdateRequest request = new("Alice", new string('B', 257));
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void MaxLength_FirstName_Passes()
    {
        AccountProfileUpdateRequest request = new(new string('A', 256), null);
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MaxLength_LastName_Passes()
    {
        AccountProfileUpdateRequest request = new(null, new string('B', 256));
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }
}

public sealed class AccountTwoFactorEnableRequestValidatorTests
{
    private readonly AccountTwoFactorEnableRequestValidator _validator = new();

    [Fact]
    public void Valid_Code_Passes()
    {
        AccountTwoFactorEnableRequest request = new("123456");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_Code_Fails()
    {
        AccountTwoFactorEnableRequest request = new("");
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Short_Code_Fails()
    {
        AccountTwoFactorEnableRequest request = new("123");
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Code);
    }

    [Fact]
    public void Long_Code_Fails()
    {
        AccountTwoFactorEnableRequest request = new("1234567");
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Code);
    }
}

public sealed class PasskeyRegistrationRequestValidatorTests
{
    private readonly PasskeyRegistrationRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        PasskeyRegistrationRequest request = new("""{"id":"abc","type":"public-key"}""", "My Key");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_CredentialJson_Fails()
    {
        PasskeyRegistrationRequest request = new("", "My Key");
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.CredentialJson);
    }

    [Fact]
    public void Null_CredentialJson_Fails()
    {
        PasskeyRegistrationRequest request = new(null!, "My Key");
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.CredentialJson);
    }

    [Fact]
    public void Null_Name_Passes()
    {
        PasskeyRegistrationRequest request = new("""{"id":"abc"}""", null);
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Long_Name_Fails()
    {
        PasskeyRegistrationRequest request = new("""{"id":"abc"}""", new string('A', 101));
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void MaxLength_Name_Passes()
    {
        PasskeyRegistrationRequest request = new("""{"id":"abc"}""", new string('A', 100));
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }
}

public sealed class PasskeyRenameRequestValidatorTests
{
    private readonly PasskeyRenameRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        PasskeyRenameRequest request = new("My YubiKey");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_Name_Fails()
    {
        PasskeyRenameRequest request = new("");
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Null_Name_Fails()
    {
        PasskeyRenameRequest request = new(null!);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Long_Name_Fails()
    {
        PasskeyRenameRequest request = new(new string('A', 101));
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void MaxLength_Name_Passes()
    {
        PasskeyRenameRequest request = new(new string('A', 100));
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }
}
