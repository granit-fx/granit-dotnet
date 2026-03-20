using FluentValidation.Results;
using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Validators;

public sealed class IdentityUserUpdateRequestValidatorTests
{
    private readonly IdentityUserUpdateRequestValidator _validator = new();

    // -------------------------------------------------------------------------
    // Valid request — all null
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_AllNullFields_ReturnsValid()
    {
        IdentityUserUpdateRequest request = new(null, null, null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Valid request — with values
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidRequestWithValues_ReturnsValid()
    {
        var attributes = new Dictionary<string, string?> { ["department"] = "engineering" };
        IdentityUserUpdateRequest request = new("jdoe@example.com", "John", "Doe", attributes);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Email — invalid format
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_InvalidEmailFormat_Fails()
    {
        IdentityUserUpdateRequest request = new("not-an-email", null, null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserUpdateRequest.Email));
    }

    // -------------------------------------------------------------------------
    // Email — exceeds max length
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_EmailExceedsMaxLength_Fails()
    {
        string longEmail = new('x', IdentityUserUpdateRequestValidator.MaxEmailLength + 1);
        IdentityUserUpdateRequest request = new(longEmail, null, null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserUpdateRequest.Email));
    }

    // -------------------------------------------------------------------------
    // FirstName — exceeds max length
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_FirstNameExceedsMaxLength_Fails()
    {
        string longName = new('x', IdentityUserUpdateRequestValidator.MaxNameLength + 1);
        IdentityUserUpdateRequest request = new(null, longName, null, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserUpdateRequest.FirstName));
    }

    // -------------------------------------------------------------------------
    // LastName — exceeds max length
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_LastNameExceedsMaxLength_Fails()
    {
        string longName = new('x', IdentityUserUpdateRequestValidator.MaxNameLength + 1);
        IdentityUserUpdateRequest request = new(null, null, longName, null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserUpdateRequest.LastName));
    }

    // -------------------------------------------------------------------------
    // Attributes — exceeds max custom attributes
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_AttributesExceedsMaxCount_Fails()
    {
        var attributes = Enumerable.Range(1, IdentityUserUpdateRequestValidator.MaxCustomAttributes + 1)
            .ToDictionary(i => $"key-{i}", i => (string?)$"value-{i}");
        IdentityUserUpdateRequest request = new(null, null, null, attributes);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(IdentityUserUpdateRequest.Attributes));
    }

    // -------------------------------------------------------------------------
    // Attributes — null is valid
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_NullAttributes_ReturnsValid()
    {
        IdentityUserUpdateRequest request = new("jdoe@example.com", "John", "Doe", null);

        ValidationResult result = _validator.Validate(request);

        result.IsValid.ShouldBeTrue();
    }
}
