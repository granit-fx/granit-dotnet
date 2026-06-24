// =============================================================================
// Tests - SpanishIdentifierValidatorExtensions
// =============================================================================
// SpanishNif:        8 digits + 1 control letter (mod 23)
// SpanishNie:        X/Y/Z + 7 digits + 1 control letter (prefix→digit, then NIF)
// SpanishCif:        1 org letter + 7 digits + 1 control char (digit or letter)
// SpanishPostalCode: 5 digits, provinces 01–52
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class SpanishIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // SpanishNif
    // =========================================================================
    // Control letters: "TRWAGMYFPDXBNJZSQVHLCKE"
    //   "12345678Z": 12345678%23=14, letter[14]=Z ✓
    //   "00000000T": 0%23=0, letter[0]=T ✓
    //   "99999999R": 99999999%23=1, letter[1]=R ✓

    [Theory]
    [InlineData("12345678Z")]          // 12345678%23=14 → Z
    [InlineData("00000000T")]          // 0%23=0 → T
    [InlineData("99999999R")]          // 99999999%23=1 → R
    public void SpanishNif_ValidValues_PassValidation(string nif)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SpanishNif();

        ValidationResult result = validator.Validate(new TestModel(nif));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678A")]          // wrong letter (should be Z)
    [InlineData("1234567Z")]           // 7 digits — too short
    [InlineData("123456789Z")]         // 9 digits — too long
    public void SpanishNif_InvalidValues_FailValidation(string? nif)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SpanishNif();

        ValidationResult result = validator.Validate(new TestModel(nif));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:SpanishNif");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:SpanishNif");
    }

    // =========================================================================
    // SpanishNie
    // =========================================================================
    // Prefix replacement: X→0, Y→1, Z→2, then NIF control algorithm.
    //   "X0000000T": 0%23=0 → T ✓
    //   "Y0000000Z": 10000000%23=14 → Z ✓
    //   "Z0000000M": 20000000%23=5 → M ✓

    [Theory]
    [InlineData("X0000000T")]          // X→0, 0000000%23=0 → T
    [InlineData("Y0000000Z")]          // Y→1, 10000000%23=14 → Z
    [InlineData("Z0000000M")]          // Z→2, 20000000%23=5 → M
    public void SpanishNie_ValidValues_PassValidation(string nie)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SpanishNie();

        ValidationResult result = validator.Validate(new TestModel(nie));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("A0000000T")]          // invalid prefix (must be X, Y, or Z)
    [InlineData("X000000T")]           // 6 digits — too short (total 8 chars)
    [InlineData("X0000000A")]          // wrong control letter (should be T)
    public void SpanishNie_InvalidValues_FailValidation(string? nie)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SpanishNie();

        ValidationResult result = validator.Validate(new TestModel(nie));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:SpanishNie");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:SpanishNie");
    }

    // =========================================================================
    // SpanishCif
    // =========================================================================
    // Format: org letter + 7 digits + control char.
    // Digit-only types (A, B, E, H): control must be digit.
    // Letter-only types (K, P, Q, S): control must be letter (0→J,1→A,...).
    //   "A12345674": type A (digit), control=4 ✓
    //   "B12345674": type B (digit), control=4 ✓
    //   "P1234567D": type P (letter), control value=4→D ✓

    [Theory]
    [InlineData("A12345674")]          // digit-only type A, control=4
    [InlineData("B12345674")]          // digit-only type B, control=4
    [InlineData("P1234567D")]          // letter-only type P, control value 4→D
    public void SpanishCif_ValidValues_PassValidation(string cif)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SpanishCif();

        ValidationResult result = validator.Validate(new TestModel(cif));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Z1234567A")]          // Z not in valid org type set
    [InlineData("12345678A")]          // starts with digit — no org type letter
    [InlineData("A12345670")]          // wrong control digit (should be 4)
    public void SpanishCif_InvalidValues_FailValidation(string? cif)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SpanishCif();

        ValidationResult result = validator.Validate(new TestModel(cif));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:SpanishCif");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:SpanishCif");
    }

    // =========================================================================
    // SpanishPostalCode
    // =========================================================================
    // Regex: ^(0[1-9]|[1-4]\d|5[0-2])\d{3}$ — provinces 01–52

    [Theory]
    [InlineData("01001")]              // province 01 — lowest valid
    [InlineData("28001")]              // Madrid
    [InlineData("08001")]              // Barcelona
    [InlineData("52001")]              // Melilla — highest valid province
    public void SpanishPostalCode_ValidValues_PassValidation(string code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SpanishPostalCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00001")]              // province 00 — does not exist
    [InlineData("53001")]              // province 53 — exceeds max (52)
    [InlineData("1234")]               // 4 digits — too short
    [InlineData("123456")]             // 6 digits — too long
    public void SpanishPostalCode_InvalidValues_FailValidation(string? code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SpanishPostalCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:SpanishPostalCode");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:SpanishPostalCode");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
