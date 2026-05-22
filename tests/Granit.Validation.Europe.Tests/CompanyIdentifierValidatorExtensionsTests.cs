// =============================================================================
// Tests - CompanyIdentifierValidatorExtensions
// =============================================================================
// FrenchSiren:  9 digits, Luhn check
// FrenchSiret: 14 digits, Luhn check on all 14
// BelgianBce:  10 digits, check = 97 − (first 8 mod 97)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class CompanyIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // FrenchSiren
    // =========================================================================
    // Valid values: publicly known SIRENs verified with Luhn algorithm.
    //   732829320 — Renault SA (Luhn sum=40)
    //   356000000 — Orange / France Télécom (Luhn sum=10)

    [Theory]
    [InlineData("732829320")]              // Renault SA
    [InlineData("356000000")]              // Orange
    [InlineData("732 829 320")]            // spaces stripped
    public void FrenchSiren_ValidValues_PassValidation(string siren)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchSiren();

        ValidationResult result = validator.Validate(new TestModel(siren));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("73282932")]               // 8 digits — too short
    [InlineData("7328293200")]             // 10 digits — too long
    [InlineData("732829321")]              // wrong Luhn check
    [InlineData("73282932A")]              // non-digit character
    public void FrenchSiren_InvalidValues_FailValidation(string? siren)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchSiren();

        ValidationResult result = validator.Validate(new TestModel(siren));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidFrenchSiren");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidFrenchSiren");
    }

    // =========================================================================
    // FrenchSiret
    // =========================================================================
    // Valid values: SIREN + NIC, Luhn on all 14 digits.
    //   73282932000074 — Renault SA siège (Luhn sum=50)

    [Theory]
    [InlineData("73282932000074")]          // Renault SA siège
    [InlineData("732 829 320 00074")]       // spaces stripped
    public void FrenchSiret_ValidValues_PassValidation(string siret)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchSiret();

        ValidationResult result = validator.Validate(new TestModel(siret));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7328293200007")]           // 13 digits — too short
    [InlineData("732829320000745")]         // 15 digits — too long
    [InlineData("73282932000075")]          // wrong Luhn check
    [InlineData("7328293200007A")]          // non-digit character
    public void FrenchSiret_InvalidValues_FailValidation(string? siret)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchSiret();

        ValidationResult result = validator.Validate(new TestModel(siret));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidFrenchSiret");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidFrenchSiret");
    }

    // =========================================================================
    // BelgianBce
    // =========================================================================
    // Valid test values computed from algorithm:
    //   check = 97 − (first 8 digits mod 97)
    //   "0100000070": first8=01000000=1000000, 10^6 mod 97=27, key=70
    //   "0000000097": first8=00000000=0, 0 mod 97=0, key=97

    [Theory]
    [InlineData("0100000070")]             // first8=1000000, mod97=27, key=70
    [InlineData("0000000097")]             // first8=0, mod97=0, key=97
    [InlineData("0100.000.070")]           // formatted with dots
    public void BelgianBce_ValidValues_PassValidation(string bce)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianBce();

        ValidationResult result = validator.Validate(new TestModel(bce));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("010000007")]              // 9 digits — too short
    [InlineData("01000000700")]            // 11 digits — too long
    [InlineData("0100000071")]             // wrong check (should be 70)
    public void BelgianBce_InvalidValues_FailValidation(string? bce)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianBce();

        ValidationResult result = validator.Validate(new TestModel(bce));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidBelgianBce");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidBelgianBce");
    }

    // =========================================================================
    // FrenchNafCode
    // =========================================================================

    [Theory]
    [InlineData("6201Z")]          // software development
    [InlineData("8621Z")]          // general medical practice
    [InlineData("0111Z")]          // cereal farming
    [InlineData("6201z")]          // lowercase — normalised to uppercase
    public void FrenchNafCode_ValidValues_PassValidation(string code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchNafCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("620Z")]           // 4 chars — too short
    [InlineData("6201ZZ")]         // 6 chars — too long
    [InlineData("ABCDZ")]          // letters before the letter suffix — invalid
    [InlineData("62011")]          // ends with digit, not letter
    public void FrenchNafCode_InvalidValues_FailValidation(string? code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchNafCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidFrenchNafCode");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidFrenchNafCode");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
