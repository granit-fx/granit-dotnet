// =============================================================================
// Tests - GermanIdentifierValidatorExtensions
// =============================================================================
// GermanSteuerId:    11 digits, ISO 7064 MOD 11,10 check digit, digit distribution rule
// GermanPostalCode:  5 digits, 00001–99999 (00000 excluded)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class GermanIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // GermanSteuerId
    // =========================================================================
    // Valid test values computed from algorithm:
    //   11 digits, first non-zero, exactly one digit appears twice in pos 1-10,
    //   exactly one digit absent, check digit via ISO 7064 MOD 11,10.
    //   "86095742719": dist ok, check=9 ✓
    //   "47036892816": dist ok, check=6 ✓
    //   "87631405090": dist ok, check=0 ✓

    [Theory]
    [InlineData("86095742719")]        // valid distribution, check digit 9
    [InlineData("47036892816")]        // valid distribution, check digit 6
    [InlineData("87631405090")]        // valid distribution, check digit 0
    public void GermanSteuerId_ValidValues_PassValidation(string steuerId)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).GermanSteuerId();

        ValidationResult result = validator.Validate(new TestModel(steuerId));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567890")]         // 10 digits — too short
    [InlineData("123456789012")]       // 12 digits — too long
    [InlineData("00000000001")]        // starts with 0
    [InlineData("12345678901")]        // invalid digit distribution (each digit 0-9 appears once)
    [InlineData("86095742710")]        // valid distribution but wrong check digit (should be 9)
    public void GermanSteuerId_InvalidValues_FailValidation(string? steuerId)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).GermanSteuerId();

        ValidationResult result = validator.Validate(new TestModel(steuerId));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidGermanSteuerId");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidGermanSteuerId");
    }

    // =========================================================================
    // GermanPostalCode
    // =========================================================================

    [Theory]
    [InlineData("10115")]              // Berlin
    [InlineData("80331")]              // Munich
    [InlineData("01067")]              // Dresden — starts with 0
    [InlineData("99998")]              // near upper bound
    public void GermanPostalCode_ValidValues_PassValidation(string code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).GermanPostalCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00000")]              // explicitly excluded
    [InlineData("1234")]               // 4 digits — too short
    [InlineData("123456")]             // 6 digits — too long
    [InlineData("ABCDE")]             // not digits
    public void GermanPostalCode_InvalidValues_FailValidation(string? code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).GermanPostalCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidGermanPostalCode");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidGermanPostalCode");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
