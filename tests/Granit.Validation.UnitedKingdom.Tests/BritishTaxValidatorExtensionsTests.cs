// =============================================================================
// Tests - BritishTaxValidatorExtensions
// =============================================================================
// UTR:                10 digits, MOD 11 check digit (digit 1)
// UkVat:              GB + 9/12 digits, MOD 97 check, or GD/HA prefixes
// CompaniesHouseNumber: 8 chars (digits or prefix + digits)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.UnitedKingdom.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.UnitedKingdom.Tests;

public sealed class BritishTaxValidatorExtensionsTests
{
    // =========================================================================
    // UniqueTaxpayerReference
    // =========================================================================

    [Theory]
    [InlineData("1234567895")]                       // Valid UTR
    [InlineData("9000000001")]                       // Valid UTR
    [InlineData("4 000 000 009")]                    // With spaces
    public void UniqueTaxpayerReference_ValidValues_PassValidation(string utr)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).UniqueTaxpayerReference();

        ValidationResult result = validator.Validate(new TestModel(utr));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0955839661")]                       // Wrong check digit
    [InlineData("123456789")]                        // Too short (9 digits)
    [InlineData("12345678901")]                      // Too long (11 digits)
    [InlineData("ABCDEFGHIJ")]                       // Non-numeric
    public void UniqueTaxpayerReference_InvalidValues_FailValidation(string? utr)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).UniqueTaxpayerReference();

        ValidationResult result = validator.Validate(new TestModel(utr));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UkUtr");
    }

    // =========================================================================
    // UkVat
    // =========================================================================

    [Theory]
    [InlineData("GB980780684")]                      // Standard 9-digit
    [InlineData("GB 980 780 684")]                   // With spaces
    [InlineData("GB980780684000")]                   // Branch trader (12 digits)
    [InlineData("GD001")]                            // Government department
    [InlineData("GD499")]                            // Government department max
    [InlineData("HA500")]                            // Health authority
    [InlineData("HA999")]                            // Health authority max
    public void UkVat_ValidValues_PassValidation(string vat)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).UkVat();

        ValidationResult result = validator.Validate(new TestModel(vat));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("GB123456789")]                      // Invalid check digits
    [InlineData("FR980780684")]                      // Wrong country prefix
    [InlineData("GB12345678")]                       // Too short (8 digits)
    [InlineData("GB0123456789")]                     // Starts with 0
    [InlineData("GD500")]                            // GD out of range
    [InlineData("HA499")]                            // HA out of range
    [InlineData("980780684")]                        // Missing GB prefix
    public void UkVat_InvalidValues_FailValidation(string? vat)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).UkVat();

        ValidationResult result = validator.Validate(new TestModel(vat));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UkVat");
    }

    // =========================================================================
    // CompaniesHouseNumber
    // =========================================================================

    [Theory]
    [InlineData("01234567")]                         // All digits
    [InlineData("OC301234")]                         // OC prefix (LLP)
    [InlineData("SC123456")]                         // SC prefix (Scotland)
    [InlineData("NI123456")]                         // NI prefix (Northern Ireland)
    [InlineData("IP123456")]                         // IP prefix
    public void CompaniesHouseNumber_ValidValues_PassValidation(string number)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).CompaniesHouseNumber();

        ValidationResult result = validator.Validate(new TestModel(number));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]                          // Too short (7 digits)
    [InlineData("123456789")]                        // Too long (9 digits)
    [InlineData("AB123456")]                         // Invalid prefix AB
    [InlineData("OC12345")]                          // Prefix + only 5 digits
    [InlineData("OC1234567")]                        // Prefix + 7 digits
    public void CompaniesHouseNumber_InvalidValues_FailValidation(string? number)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).CompaniesHouseNumber();

        ValidationResult result = validator.Validate(new TestModel(number));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UkCompaniesHouseNumber");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
