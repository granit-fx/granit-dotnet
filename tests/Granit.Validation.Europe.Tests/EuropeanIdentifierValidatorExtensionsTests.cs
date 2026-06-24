// =============================================================================
// Tests - EuropeanIdentifierValidatorExtensions
// =============================================================================
// Eori: 2-letter ISO 3166-1 alpha-2 country code + 1–15 alphanumeric chars
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class EuropeanIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // Eori
    // =========================================================================
    // Valid format: 2-letter country code + 1–15 alphanumeric characters.

    [Theory]
    [InlineData("FR12345678901")]           // France — 11 digit national part
    [InlineData("DE123456789")]             // Germany — 9 digit national part
    [InlineData("NLABCDEF12345")]           // Netherlands — mixed alphanumeric
    [InlineData("BE0123456789")]            // Belgium — 10 digit national part
    [InlineData("fr12345678901")]           // lowercase — normalised to uppercase
    [InlineData("IT1")]                     // minimum national part (1 char)
    public void Eori_ValidValues_PassValidation(string value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Eori();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]                   // no country code prefix
    [InlineData("FR")]                      // country code only — no national part
    [InlineData("A1234567890")]             // single-letter country code — invalid
    [InlineData("FR1234567890123456")]      // 16-char national part — too long
    public void Eori_InvalidValues_FailValidation(string? value)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Eori();

        ValidationResult result = validator.Validate(new TestModel(value));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:Eori");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:Eori");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
