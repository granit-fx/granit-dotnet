// =============================================================================
// Tests - PaymentValidatorExtensions
// =============================================================================
// BicSwift:              8 or 11 alphanumeric chars, ISO 9362 format
// SepaCreditorIdentifier: CC + 2 check + 3 CBA + national ID, ISO 7064 MOD 97-10
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Finance.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Finance.Tests;

public sealed class FinancePaymentValidatorExtensionsTests
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

    // =========================================================================
    // Iban
    // =========================================================================

    [Theory]
    [InlineData("BE68539007547034")]
    [InlineData("BE68 5390 0754 7034")]
    [InlineData("FR7630006000011234567890189")]
    [InlineData("DE89370400440532013000")]
    public void Iban_ValidValues_PassValidation(string iban)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Iban();

        validator.Validate(new TestModel(iban)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BE68539007547035")]   // wrong check digit
    [InlineData("123456789")]          // no country code
    public void Iban_InvalidValues_FailValidation(string? iban)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Iban();

        ValidationResult result = validator.Validate(new TestModel(iban));
        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:Iban");
    }

    // =========================================================================
    // AbaRouting
    // =========================================================================

    [Theory]
    [InlineData("021000021")]   // JPMorgan Chase
    [InlineData("011000015")]   // Federal Reserve Boston
    public void AbaRouting_ValidValues_PassValidation(string aba)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).AbaRouting();

        validator.Validate(new TestModel(aba)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("021000022")]   // bad checksum
    [InlineData("12345678")]    // 8 digits
    [InlineData("02100002A")]   // non-digit
    public void AbaRouting_InvalidValues_FailValidation(string? aba)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).AbaRouting();

        ValidationResult result = validator.Validate(new TestModel(aba));
        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:AbaRouting");
    }

    // =========================================================================
    // Bsb
    // =========================================================================

    [Theory]
    [InlineData("082902")]
    [InlineData("082-902")]
    public void Bsb_ValidValues_PassValidation(string bsb)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Bsb();

        validator.Validate(new TestModel(bsb)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]    // 5 digits
    [InlineData("abcdef")]
    public void Bsb_InvalidValues_FailValidation(string? bsb)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Bsb();

        ValidationResult result = validator.Validate(new TestModel(bsb));
        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:Bsb");
    }

    // =========================================================================
    // CanadianRouting
    // =========================================================================

    [Theory]
    [InlineData("00012345")]
    [InlineData("12345-678")]
    [InlineData("000123456")]   // 9-digit electronic form
    public void CanadianRouting_ValidValues_PassValidation(string routing)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).CanadianRouting();

        validator.Validate(new TestModel(routing)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]    // 7 digits
    [InlineData("abcdefgh")]
    public void CanadianRouting_InvalidValues_FailValidation(string? routing)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).CanadianRouting();

        ValidationResult result = validator.Validate(new TestModel(routing));
        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:CanadianRouting");
    }

    // =========================================================================
    // Ifsc
    // =========================================================================

    [Theory]
    [InlineData("SBIN0001234")]
    [InlineData("HDFC0CAGSBK")]
    public void Ifsc_ValidValues_PassValidation(string ifsc)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Ifsc();

        validator.Validate(new TestModel(ifsc)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SBIN1001234")]   // 5th char not 0
    [InlineData("SBI0001234")]    // too short
    public void Ifsc_InvalidValues_FailValidation(string? ifsc)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Ifsc();

        ValidationResult result = validator.Validate(new TestModel(ifsc));
        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:Ifsc");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
