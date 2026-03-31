using FluentValidation.TestHelper;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Validators;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests.Validators;

public sealed class AccountPasskeyLoginRequestValidatorTests
{
    private readonly AccountPasskeyLoginRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_Passes()
    {
        AccountPasskeyLoginRequest request = new("""{"id":"abc","type":"public-key"}""");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_CredentialJson_Fails()
    {
        AccountPasskeyLoginRequest request = new("");
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.CredentialJson);
    }

    [Fact]
    public void Null_CredentialJson_Fails()
    {
        AccountPasskeyLoginRequest request = new(null!);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.CredentialJson);
    }

    [Fact]
    public void Whitespace_CredentialJson_Fails()
    {
        AccountPasskeyLoginRequest request = new("   ");
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.CredentialJson);
    }

    [Fact]
    public void SingleCharacter_CredentialJson_Passes()
    {
        AccountPasskeyLoginRequest request = new("{");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }
}
