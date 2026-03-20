using FluentValidation.Results;
using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Validators;

public sealed class IdentitySetTemporaryPasswordRequestValidatorTests
{
    private readonly IdentitySetTemporaryPasswordRequestValidator _validator = new();

    // -------------------------------------------------------------------------
    // Valid password
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidPassword_ReturnsValid()
    {
        IdentitySetTemporaryPasswordRequest request = new("P@ssw0rd!");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Password — empty
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_EmptyPassword_Fails()
    {
        IdentitySetTemporaryPasswordRequest request = new("");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentitySetTemporaryPasswordRequest.Password));
    }

    // -------------------------------------------------------------------------
    // Password — too short
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_PasswordTooShort_Fails()
    {
        string shortPassword = new('x', IdentitySetTemporaryPasswordRequestValidator.MinPasswordLength - 1);
        IdentitySetTemporaryPasswordRequest request = new(shortPassword);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentitySetTemporaryPasswordRequest.Password));
    }

    // -------------------------------------------------------------------------
    // Password — too long
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_PasswordTooLong_Fails()
    {
        string longPassword = new('x', IdentitySetTemporaryPasswordRequestValidator.MaxPasswordLength + 1);
        IdentitySetTemporaryPasswordRequest request = new(longPassword);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentitySetTemporaryPasswordRequest.Password));
    }
}
