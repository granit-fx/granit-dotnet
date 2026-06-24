// =============================================================================
// Tests - ItalianIdentifierValidatorExtensions
// =============================================================================
// ItalianCodiceFiscale: 16 alphanumeric chars, check character
// ItalianPartitaIva:    11 digits, Luhn-variant check digit
// ItalianPostalCode:    5 digits, prefix 00–98 (CAP)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class ItalianIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // ItalianCodiceFiscale
    // =========================================================================
    // Valid values: computed from the check-character algorithm.
    //   RSSMRA85M01H501Q — Mario Rossi, born 01/08/1985, Roma

    [Theory]
    [InlineData("RSSMRA85M01H501Q")]       // valid check character Q
    [InlineData("rssmra85m01h501q")]        // lowercase — normalised to uppercase
    [InlineData("BNCLRD85M01H501Y")]        // different name, valid check Y
    public void ItalianCodiceFiscale_ValidValues_PassValidation(string value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).ItalianCodiceFiscale();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("RSSMRA85M01H501Z")]        // wrong check character (should be Q)
    [InlineData("RSSMRA85M01H50")]          // 15 chars — too short
    [InlineData("1234567890123456")]         // all digits — invalid format
    [InlineData("RSSMRA85M01H501QX")]       // 18 chars — too long
    public void ItalianCodiceFiscale_InvalidValues_FailValidation(string? value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).ItalianCodiceFiscale();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:ItalianCodiceFiscale");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:ItalianCodiceFiscale");
    }

    // =========================================================================
    // ItalianPartitaIva
    // =========================================================================
    // Valid values: 11 digits with Luhn-variant check digit.
    //   12345670785 — verified against the algorithm

    [Theory]
    [InlineData("12345670785")]             // valid Luhn-variant check
    [InlineData("123 4567 0785")]           // spaces stripped
    public void ItalianPartitaIva_ValidValues_PassValidation(string value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).ItalianPartitaIva();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890")]              // 10 digits — too short
    [InlineData("123456789012")]            // 12 digits — too long
    [InlineData("12345670786")]             // wrong check digit (should be 5)
    [InlineData("00000000000")]             // all zeros — explicitly rejected
    [InlineData("1234567078A")]             // non-digit character
    public void ItalianPartitaIva_InvalidValues_FailValidation(string? value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).ItalianPartitaIva();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:ItalianPartitaIva");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:ItalianPartitaIva");
    }

    // =========================================================================
    // ItalianPostalCode (CAP)
    // =========================================================================
    // Valid range: 5 digits, prefix 00–98.

    [Theory]
    [InlineData("00100")]                   // Roma
    [InlineData("20100")]                   // Milano
    [InlineData("80100")]                   // Napoli
    [InlineData("98168")]                   // Messina (highest prefix 98)
    public void ItalianPostalCode_ValidValues_PassValidation(string value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).ItalianPostalCode();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("99000")]                   // prefix 99 — out of range
    [InlineData("1234")]                    // 4 digits — too short
    [InlineData("ABCDE")]                   // letters — invalid
    [InlineData("123456")]                  // 6 digits — too long
    public void ItalianPostalCode_InvalidValues_FailValidation(string? value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).ItalianPostalCode();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:ItalianPostalCode");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:ItalianPostalCode");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
