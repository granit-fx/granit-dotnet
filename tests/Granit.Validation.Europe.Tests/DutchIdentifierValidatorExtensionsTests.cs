// =============================================================================
// Tests - DutchIdentifierValidatorExtensions
// =============================================================================
// DutchBsn:      9 digits, elfproef (9*d1+8*d2+...+2*d8-1*d9 mod 11 == 0, != 0)
// DutchKvk:      8 digits, format-only
// DutchPostcode:  4 digits (1000-9999) + optional space + 2 letters (SA/SD/SS excluded)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class DutchIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // DutchBsn
    // =========================================================================
    // Valid test values computed from elfproef:
    //   "111222333": 9+8+7+12+10+8+9+6-3=66, 66%11=0 ✓, !=0 ✓
    //   "123456782": known valid BSN, sum=154, 154%11=0 ✓, !=0 ✓

    [Theory]
    [InlineData("111222333")]          // elfproef: sum=66, 66%11=0 ✓
    [InlineData("123456782")]          // elfproef: sum=154, 154%11=0 ✓
    public void DutchBsn_ValidValues_PassValidation(string bsn)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).DutchBsn();

        ValidationResult result = validator.Validate(new TestModel(bsn));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678")]           // 8 digits — too short
    [InlineData("1234567890")]         // 10 digits — too long
    [InlineData("000000000")]          // elfproef sum=0, rejected (must be != 0)
    [InlineData("123456789")]          // elfproef fails (sum not divisible by 11)
    public void DutchBsn_InvalidValues_FailValidation(string? bsn)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).DutchBsn();

        ValidationResult result = validator.Validate(new TestModel(bsn));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:DutchBsn");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:DutchBsn");
    }

    // =========================================================================
    // DutchKvk
    // =========================================================================

    [Theory]
    [InlineData("12345678")]
    [InlineData("00000001")]
    public void DutchKvk_ValidValues_PassValidation(string kvk)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).DutchKvk();

        ValidationResult result = validator.Validate(new TestModel(kvk));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]            // 7 digits — too short
    [InlineData("123456789")]          // 9 digits — too long
    [InlineData("ABCDEFGH")]          // not digits
    public void DutchKvk_InvalidValues_FailValidation(string? kvk)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).DutchKvk();

        ValidationResult result = validator.Validate(new TestModel(kvk));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:DutchKvk");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:DutchKvk");
    }

    // =========================================================================
    // DutchPostcode
    // =========================================================================

    [Theory]
    [InlineData("1011 AB")]            // with space
    [InlineData("1011AB")]             // without space
    [InlineData("9999ZZ")]             // near upper bound
    [InlineData("1000AA")]             // lower bound
    public void DutchPostcode_ValidValues_PassValidation(string postcode)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).DutchPostcode();

        ValidationResult result = validator.Validate(new TestModel(postcode));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0000AA")]             // starts with 0 — invalid
    [InlineData("1011SA")]             // SA forbidden
    [InlineData("1011SD")]             // SD forbidden
    [InlineData("1011SS")]             // SS forbidden
    [InlineData("12345")]              // no letter part
    public void DutchPostcode_InvalidValues_FailValidation(string? postcode)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).DutchPostcode();

        ValidationResult result = validator.Validate(new TestModel(postcode));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:DutchPostcode");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:DutchPostcode");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
