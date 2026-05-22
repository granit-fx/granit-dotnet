// =============================================================================
// Tests - TaxIdentifierValidatorExtensions
// =============================================================================
// BelgianVat:  BE + BCE number (10 digits, leading 0 optional)
// FrenchVat:   FR + 2-digit key + 9-digit SIREN, key = (12+3*(SIREN%97))%97
// EuropeanVat: dispatch by country code, FR/BE algorithmic, others format only
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class TaxIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // BelgianVat
    // =========================================================================
    // Belgian VAT = "BE" + BCE number. BCE validated via BceAlgorithm.
    //   "BE0100000070": BCE=0100000070, first8=1000000, mod97=27, key=70 ✓
    //   "BE100000070":  9-digit form (leading 0 implicit), same BCE ✓

    [Theory]
    [InlineData("BE0100000070")]           // 10-digit form
    [InlineData("BE100000070")]            // 9-digit form (leading 0 implicit)
    public void BelgianVat_ValidValues_PassValidation(string vat)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianVat();

        ValidationResult result = validator.Validate(new TestModel(vat));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BE0100000071")]           // wrong BCE check (should be 70)
    [InlineData("FR0100000070")]           // wrong country — not BE
    [InlineData("BE010000071")]            // 9-digit form — wrong check (should be 07, not 71)
    public void BelgianVat_InvalidValues_FailValidation(string? vat)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianVat();

        ValidationResult result = validator.Validate(new TestModel(vat));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidBelgianVat");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidBelgianVat");
    }

    // =========================================================================
    // FrenchVat
    // =========================================================================
    // key = (12 + 3 × (SIREN mod 97)) mod 97
    //   SIREN=732829320: 732829320 mod 97=43, key=(12+129)%97=141%97=44 → FR44732829320
    //   SIREN=356000000: 356000000 mod 97=9,  key=(12+27)%97=39           → FR39356000000

    [Theory]
    [InlineData("FR44732829320")]          // SIREN=732829320, key=44
    [InlineData("FR39356000000")]          // SIREN=356000000, key=39
    [InlineData("fr44732829320")]          // lowercase accepted
    public void FrenchVat_ValidValues_PassValidation(string vat)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchVat();

        ValidationResult result = validator.Validate(new TestModel(vat));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FR45732829320")]          // wrong key (should be 44)
    [InlineData("DE44732829320")]          // wrong country prefix
    [InlineData("FR44732829")]             // too short — SIREN must be 9 digits
    [InlineData("FRAA732829320")]          // alpha key — not supported by this validator
    public void FrenchVat_InvalidValues_FailValidation(string? vat)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchVat();

        ValidationResult result = validator.Validate(new TestModel(vat));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidFrenchVat");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidFrenchVat");
    }

    // =========================================================================
    // EuropeanVat
    // =========================================================================

    [Theory]
    [InlineData("FR44732829320")]          // France — algorithmic check
    [InlineData("BE0100000070")]           // Belgium — algorithmic check
    [InlineData("DE123456789")]            // Germany — format check (9 digits)
    [InlineData("ATU12345678")]            // Austria — format check (U + 8 digits)
    [InlineData("DK12345678")]             // Denmark — format check (8 digits)
    [InlineData("NL123456789B01")]         // Netherlands — format check
    public void EuropeanVat_ValidValues_PassValidation(string vat)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).EuropeanVat();

        ValidationResult result = validator.Validate(new TestModel(vat));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XX123456789")]            // unsupported country code
    [InlineData("FR45732829320")]          // FR with wrong key
    [InlineData("BE0100000071")]           // BE with wrong BCE check
    [InlineData("DE12345678")]             // DE too short (8 not 9 digits)
    public void EuropeanVat_InvalidValues_FailValidation(string? vat)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).EuropeanVat();

        ValidationResult result = validator.Validate(new TestModel(vat));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidEuropeanVat");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidEuropeanVat");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
