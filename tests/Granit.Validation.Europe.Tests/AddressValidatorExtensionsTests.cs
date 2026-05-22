// =============================================================================
// Tests - AddressValidatorExtensions
// =============================================================================
// FrenchPostalCode:  5 digits, 01000–99999
// BelgianPostalCode: 4 digits, 1000–9999
// FrenchInseeCode:   5 chars, dept (01–99 | 2A/2B | 971–976) + commune
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class AddressValidatorExtensionsTests
{
    // =========================================================================
    // FrenchPostalCode
    // =========================================================================

    [Theory]
    [InlineData("75001")]          // Paris 1er
    [InlineData("01000")]          // Ain — starts with 01
    [InlineData("69001")]          // Lyon
    [InlineData("13001")]          // Marseille
    [InlineData("97200")]          // Martinique (DOM)
    [InlineData("20000")]          // Corse — historical 20xxx still valid
    public void FrenchPostalCode_ValidValues_PassValidation(string code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchPostalCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]           // 4 digits — too short
    [InlineData("123456")]         // 6 digits — too long
    [InlineData("00001")]          // starts with 00 — not a valid French code
    [InlineData("ABCDE")]          // not digits
    public void FrenchPostalCode_InvalidValues_FailValidation(string? code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchPostalCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidFrenchPostalCode");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidFrenchPostalCode");
    }

    // =========================================================================
    // BelgianPostalCode
    // =========================================================================

    [Theory]
    [InlineData("1000")]           // Brussels
    [InlineData("9000")]           // Ghent
    [InlineData("4000")]           // Liège
    [InlineData("1234")]
    public void BelgianPostalCode_ValidValues_PassValidation(string code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianPostalCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("999")]            // 3 digits — too short
    [InlineData("10000")]          // 5 digits — too long
    [InlineData("0999")]           // starts with 0 — invalid Belgian code
    [InlineData("ABCD")]           // not digits
    public void BelgianPostalCode_InvalidValues_FailValidation(string? code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianPostalCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidBelgianPostalCode");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidBelgianPostalCode");
    }

    // =========================================================================
    // FrenchInseeCode
    // =========================================================================

    [Theory]
    [InlineData("75056")]          // Paris (dept 75, commune 056)
    [InlineData("13055")]          // Marseille (dept 13, commune 055)
    [InlineData("2A004")]          // Ajaccio — Corse-du-Sud (dept 2A)
    [InlineData("2B033")]          // Bastia — Haute-Corse (dept 2B)
    [InlineData("2a004")]          // lowercase Corse — accepted (case-insensitive)
    [InlineData("97209")]          // Fort-de-France — Martinique (dept 972)
    public void FrenchInseeCode_ValidValues_PassValidation(string code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchInseeCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7505")]           // 4 chars — too short
    [InlineData("750560")]         // 6 chars — too long
    [InlineData("00056")]          // dept 00 — does not exist
    [InlineData("97056")]          // 97056 — dept 97 without DOM suffix (971–976 required)
    [InlineData("ABCDE")]          // not valid pattern
    public void FrenchInseeCode_InvalidValues_FailValidation(string? code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchInseeCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidFrenchInseeCode");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidFrenchInseeCode");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
