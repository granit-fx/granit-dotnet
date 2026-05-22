// =============================================================================
// Tests - CanadianIdentifierValidatorExtensions
// =============================================================================
// SocialInsuranceNumber:   9 digits, Luhn check, first digit != 0 or 8
// CanadianBusinessNumber:  9 digits, Luhn check
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.NorthAmerica.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.NorthAmerica.Tests;

public sealed class CanadianIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // SocialInsuranceNumber
    // =========================================================================

    [Theory]
    [InlineData("130 692 544")]                      // Valid Luhn
    [InlineData("130-692-544")]                      // With dashes
    [InlineData("130692544")]                        // No formatting
    [InlineData("972 342 406")]                      // Temporary resident (9xx)
    public void SocialInsuranceNumber_ValidValues_PassValidation(string sin)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SocialInsuranceNumber();

        ValidationResult result = validator.Validate(new TestModel(sin));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("046 454 287")]                      // Bad Luhn check digit
    [InlineData("000 000 000")]                      // Starts with 0
    [InlineData("800 000 002")]                      // Starts with 8
    [InlineData("12345678")]                         // Too short
    [InlineData("1234567890")]                       // Too long
    [InlineData("ABC DEF GHI")]                      // Non-numeric
    public void SocialInsuranceNumber_InvalidValues_FailValidation(string? sin)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SocialInsuranceNumber();

        ValidationResult result = validator.Validate(new TestModel(sin));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidCanadianSin");
    }

    // =========================================================================
    // CanadianBusinessNumber
    // =========================================================================

    [Theory]
    [InlineData("123456782")]                        // Valid Luhn
    [InlineData("123-456-782")]                      // With dashes
    [InlineData("123 456 782")]                      // With spaces
    public void CanadianBusinessNumber_ValidValues_PassValidation(string bn)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).CanadianBusinessNumber();

        ValidationResult result = validator.Validate(new TestModel(bn));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456789")]                        // Bad Luhn check digit
    [InlineData("12345678")]                         // Too short
    [InlineData("1234567890")]                       // Too long
    [InlineData("ABCDEFGHI")]                        // Non-numeric
    public void CanadianBusinessNumber_InvalidValues_FailValidation(string? bn)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).CanadianBusinessNumber();

        ValidationResult result = validator.Validate(new TestModel(bn));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidCanadianBusinessNumber");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
