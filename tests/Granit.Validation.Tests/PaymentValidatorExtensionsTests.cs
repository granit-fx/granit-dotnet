// =============================================================================
// Tests - PaymentValidatorExtensions
// =============================================================================
// BicSwift:              8 or 11 alphanumeric chars, ISO 9362 format
// SepaCreditorIdentifier: CC + 2 check + 3 CBA + national ID, ISO 7064 MOD 97-10
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class PaymentValidatorExtensionsTests
{
    // =========================================================================
    // BicSwift
    // =========================================================================

    [Theory]
    [InlineData("GEBABEBB")]               // 8-char BIC — BNP Paribas Fortis
    [InlineData("BNPAFRPP")]               // 8-char BIC — BNP Paribas France
    [InlineData("GEBABEBB36A")]            // 11-char BIC with branch code
    [InlineData("gebabebb")]               // lowercase — normalised to uppercase
    public void BicSwift_ValidValues_PassValidation(string bic)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BicSwift();

        ValidationResult result = validator.Validate(new TestModel(bic));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GEBA")]                   // 4 chars — too short
    [InlineData("GEBABE")]                 // 6 chars — invalid length
    [InlineData("GEBA1BBB")]              // digit in country code position (pos 5)
    [InlineData("12345678")]              // starts with digits — invalid bank code
    [InlineData("GEBABEBB36AB")]          // 12 chars — too long
    public void BicSwift_InvalidValues_FailValidation(string? bic)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BicSwift();

        ValidationResult result = validator.Validate(new TestModel(bic));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:BicSwift");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:BicSwift");
    }

    // =========================================================================
    // SepaCreditorIdentifier
    // =========================================================================
    // Check validated via ISO 7064 MOD 97-10 (same as IBAN).
    // Valid test values computed from algorithm:
    //   "BE46ZZZ000000000": rearranged=ZZZ000000000BE46 → 353535000000000111446 mod 97=1 ✓
    //   "FR20ZZZ123456":    rearranged=ZZZ123456FR20    → 353535123456152720   mod 97=1 ✓

    [Theory]
    [InlineData("BE46ZZZ000000000")]       // Belgian SCI
    [InlineData("FR20ZZZ123456")]          // French SCI
    public void SepaCreditorIdentifier_ValidValues_PassValidation(string sci)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SepaCreditorIdentifier();

        ValidationResult result = validator.Validate(new TestModel(sci));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BE47ZZZ000000000")]       // wrong check (46 → 47), mod97 ≠ 1
    [InlineData("BE92")]                   // too short (4 chars < 8 minimum)
    [InlineData("1234ZZZ000000000")]       // country code not alpha
    [InlineData("BEAAZZZ000000000")]       // check not digits
    public void SepaCreditorIdentifier_InvalidValues_FailValidation(string? sci)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SepaCreditorIdentifier();

        ValidationResult result = validator.Validate(new TestModel(sci));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:SepaCreditorIdentifier");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:SepaCreditorIdentifier");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
