// =============================================================================
// Tests - LocaleValidatorExtensions
// =============================================================================
// Iso3166Alpha2:  2 uppercase letters (ISO 3166-1 alpha-2), format check only
// Bcp47LanguageTag: lang(2–3) + optional script(4) + optional region(2), BCP 47 subset
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class LocaleValidatorExtensionsTests
{
    // =========================================================================
    // Iso3166Alpha2CountryCode
    // =========================================================================

    [Theory]
    [InlineData("BE")]
    [InlineData("FR")]
    [InlineData("DE")]
    [InlineData("US")]
    [InlineData("be")]             // lowercase — normalised to uppercase
    public void Iso3166Alpha2CountryCode_ValidValues_PassValidation(string code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Iso3166Alpha2CountryCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("B")]              // 1 letter — too short
    [InlineData("BEL")]           // 3 letters — too long (alpha-3)
    [InlineData("12")]             // digits not letters
    public void Iso3166Alpha2CountryCode_InvalidValues_FailValidation(string? code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Iso3166Alpha2CountryCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:Iso3166Alpha2");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:Iso3166Alpha2");
    }

    // =========================================================================
    // Bcp47LanguageTag
    // =========================================================================

    [Theory]
    [InlineData("fr")]             // language only
    [InlineData("en")]
    [InlineData("nl")]
    [InlineData("fr-FR")]          // language + region
    [InlineData("fr-BE")]
    [InlineData("en-US")]
    [InlineData("zh-Hans")]        // language + script
    [InlineData("zh-Hans-CN")]     // language + script + region
    [InlineData("FR-be")]          // case-insensitive
    public void Bcp47LanguageTag_ValidValues_PassValidation(string tag)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Bcp47LanguageTag();

        ValidationResult result = validator.Validate(new TestModel(tag));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("f")]              // single letter — too short
    [InlineData("fren")]           // 4-letter primary tag without dash — not valid as lang
    [InlineData("fr-B")]           // 1-letter region — too short
    [InlineData("123")]            // digits only
    [InlineData("fr-FR-extra")]    // too many subtags
    public void Bcp47LanguageTag_InvalidValues_FailValidation(string? tag)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Bcp47LanguageTag();

        ValidationResult result = validator.Validate(new TestModel(tag));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:Bcp47LanguageTag");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:Bcp47LanguageTag");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
