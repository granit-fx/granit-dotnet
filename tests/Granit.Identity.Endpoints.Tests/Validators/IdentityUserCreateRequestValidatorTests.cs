using FluentValidation.Results;
using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Validators;

public sealed class IdentityUserCreateRequestValidatorTests
{
    private readonly IdentityUserCreateRequestValidator _validator = new();

    // -------------------------------------------------------------------------
    // Valid request
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidRequest_ReturnsValid()
    {
        IdentityUserCreateRequest request = new("jdoe", "jdoe@example.com", "John", "Doe", true, "P@ssw0rd!");

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Username — empty
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_EmptyUsername_Fails()
    {
        IdentityUserCreateRequest request = new("", "jdoe@example.com", "John", "Doe", true, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserCreateRequest.Username));
    }

    // -------------------------------------------------------------------------
    // Username — exceeds max length
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_UsernameExceedsMaxLength_Fails()
    {
        string longUsername = new('x', IdentityUserCreateRequestValidator.MaxUsernameLength + 1);
        IdentityUserCreateRequest request = new(longUsername, "jdoe@example.com", "John", "Doe", true, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserCreateRequest.Username));
    }

    // -------------------------------------------------------------------------
    // Email — empty
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_EmptyEmail_Fails()
    {
        IdentityUserCreateRequest request = new("jdoe", "", "John", "Doe", true, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserCreateRequest.Email));
    }

    // -------------------------------------------------------------------------
    // Email — invalid format
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_InvalidEmailFormat_Fails()
    {
        IdentityUserCreateRequest request = new("jdoe", "not-an-email", "John", "Doe", true, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserCreateRequest.Email));
    }

    // -------------------------------------------------------------------------
    // Email — exceeds max length
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_EmailExceedsMaxLength_Fails()
    {
        string longEmail = new('x', IdentityUserCreateRequestValidator.MaxEmailLength + 1);
        IdentityUserCreateRequest request = new("jdoe", longEmail, "John", "Doe", true, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserCreateRequest.Email));
    }

    // -------------------------------------------------------------------------
    // FirstName — exceeds max length
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_FirstNameExceedsMaxLength_Fails()
    {
        string longName = new('x', IdentityUserCreateRequestValidator.MaxNameLength + 1);
        IdentityUserCreateRequest request = new("jdoe", "jdoe@example.com", longName, "Doe", true, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserCreateRequest.FirstName));
    }

    // -------------------------------------------------------------------------
    // LastName — exceeds max length
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_LastNameExceedsMaxLength_Fails()
    {
        string longName = new('x', IdentityUserCreateRequestValidator.MaxNameLength + 1);
        IdentityUserCreateRequest request = new("jdoe", "jdoe@example.com", "John", longName, true, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserCreateRequest.LastName));
    }

    // -------------------------------------------------------------------------
    // FirstName — null is valid (optional)
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_NullFirstName_ReturnsValid()
    {
        IdentityUserCreateRequest request = new("jdoe", "jdoe@example.com", null, "Doe", true, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // TemporaryPassword — too short
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_TemporaryPasswordTooShort_Fails()
    {
        string shortPassword = new('x', IdentityUserCreateRequestValidator.MinPasswordLength - 1);
        IdentityUserCreateRequest request = new("jdoe", "jdoe@example.com", "John", "Doe", true, shortPassword);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserCreateRequest.TemporaryPassword));
    }

    // -------------------------------------------------------------------------
    // TemporaryPassword — too long
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_TemporaryPasswordTooLong_Fails()
    {
        string longPassword = new('x', IdentityUserCreateRequestValidator.MaxPasswordLength + 1);
        IdentityUserCreateRequest request = new("jdoe", "jdoe@example.com", "John", "Doe", true, longPassword);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserCreateRequest.TemporaryPassword));
    }

    // -------------------------------------------------------------------------
    // TemporaryPassword — null is valid (optional)
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_NullTemporaryPassword_ReturnsValid()
    {
        IdentityUserCreateRequest request = new("jdoe", "jdoe@example.com", "John", "Doe", true, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
