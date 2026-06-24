// =============================================================================
// Tests - PaymentValidatorExtensions (Europe-specific)
// =============================================================================
// FrenchRib:             23 chars, key = 97 − (89×bank + 15×branch + 3×account) mod 97
// BelgianAccountNumber:  12 digits, key = base mod 97 (or 97 when remainder is 0)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class PaymentValidatorExtensionsTests
{
    // =========================================================================
    // FrenchRib
    // =========================================================================
    // Key formula: clé = 97 − (89 × banque + 15 × guichet + 3 × compte) mod 97
    //   "30004000010000000404529": bank=30004, branch=00001, account=00000004045
    //     → (89×30004 + 15×1 + 3×4045) mod 97 = 2682506 mod 97 = 68, key = 29 ✓
    //   "00000000000000000000097": all-zero account → mod 97 = 0, key = 97 ✓

    [Theory]
    [InlineData("30004000010000000404529")]            // numeric RIB
    [InlineData("30004 00001 00000004045 29")]         // formatted with spaces
    [InlineData("00000000000000000000097")]            // all-zero account, key = 97
    public void FrenchRib_ValidValues_PassValidation(string rib)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchRib();

        ValidationResult result = validator.Validate(new TestModel(rib));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3000400001000000040452")]             // 22 chars — too short
    [InlineData("300040000100000004045290")]           // 24 chars — too long
    [InlineData("30004000010000000404530")]            // wrong key (29 → 30)
    public void FrenchRib_InvalidValues_FailValidation(string? rib)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchRib();

        ValidationResult result = validator.Validate(new TestModel(rib));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:FrenchRib");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:FrenchRib");
    }

    // =========================================================================
    // BelgianAccountNumber
    // =========================================================================
    // Key formula: key = (first 10 digits) mod 97, or 97 when remainder is 0.
    //   "123456789002": base=1234567890, mod 97 = 2, key = 02 ✓
    //   "000000000097": base=0, mod 97 = 0, key = 97 ✓

    [Theory]
    [InlineData("123456789002")]            // mod 97 = 2, key = 02
    [InlineData("000000000097")]            // mod 97 = 0, key = 97
    [InlineData("123-4567890-02")]          // formatted with dashes
    public void BelgianAccountNumber_ValidValues_PassValidation(string account)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianAccountNumber();

        ValidationResult result = validator.Validate(new TestModel(account));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678900")]             // 11 digits — too short
    [InlineData("1234567890020")]           // 13 digits — too long
    [InlineData("123456789003")]            // wrong key (02 → 03)
    public void BelgianAccountNumber_InvalidValues_FailValidation(string? account)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianAccountNumber();

        ValidationResult result = validator.Validate(new TestModel(account));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:BelgianAccountNumber");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:BelgianAccountNumber");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
