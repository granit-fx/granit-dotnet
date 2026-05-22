// =============================================================================
// Tests - LuxembourgishIdentifierValidatorExtensions
// =============================================================================
// LuxembourgMatricule: 13 digits (YYYYMMDDXXXCC), Luhn check
// LuxembourgRcs:       letter prefix (A–J or S) + 1–6 digits
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class LuxembourgishIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // LuxembourgMatricule
    // =========================================================================
    // Valid values: 13 digits passing the Luhn algorithm.
    //   1983010100010 — computed valid check digit
    //   0000000000000 — 13 zeros, Luhn sum = 0

    [Theory]
    [InlineData("1983010100010")]           // valid Luhn check
    [InlineData("0000000000000")]           // 13 zeros — Luhn sum = 0
    [InlineData("1983 0101 00010")]         // spaces stripped
    [InlineData("1983-0101-00010")]         // dashes stripped
    public void LuxembourgMatricule_ValidValues_PassValidation(string value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).LuxembourgMatricule();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("000000000000")]            // 12 digits — too short
    [InlineData("00000000000000")]          // 14 digits — too long
    [InlineData("000000000000A")]           // non-digit character
    [InlineData("0000000000001")]           // Luhn fail (sum = 1)
    public void LuxembourgMatricule_InvalidValues_FailValidation(string? value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).LuxembourgMatricule();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidLuxembourgMatricule");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidLuxembourgMatricule");
    }

    // =========================================================================
    // LuxembourgRcs
    // =========================================================================
    // Valid format: single letter (A–J or S) + 1 to 6 digits.

    [Theory]
    [InlineData("B12345")]                  // SARL
    [InlineData("A1")]                      // SA — minimum digits
    [InlineData("S123456")]                 // succursale — maximum digits
    [InlineData("J99")]                     // coopérative
    [InlineData("b12345")]                  // lowercase — normalised to uppercase
    [InlineData("H 123")]                   // spaces stripped
    public void LuxembourgRcs_ValidValues_PassValidation(string value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).LuxembourgRcs();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Z12345")]                  // Z not a valid prefix
    [InlineData("B")]                       // no digits
    [InlineData("B1234567")]                // 7 digits — too long
    [InlineData("12345")]                   // no letter prefix
    [InlineData("K1")]                      // K not in valid range (A–J, S)
    public void LuxembourgRcs_InvalidValues_FailValidation(string? value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).LuxembourgRcs();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidLuxembourgRcs");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidLuxembourgRcs");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
