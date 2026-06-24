// =============================================================================
// Tests - AmericanIdentifierValidatorExtensions
// =============================================================================
// SSN:         AAA-GG-SSSS (area 001-899 excl. 666, group 01-99, serial 0001-9999)
// EIN:         XX-XXXXXXX (valid IRS campus prefix)
// UsStateCode: 2-letter USPS code (50 states + DC + territories + armed forces)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.NorthAmerica.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.NorthAmerica.Tests;

public sealed class AmericanIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // SocialSecurityNumber
    // =========================================================================

    [Theory]
    [InlineData("078-05-1120")]
    [InlineData("001-01-0001")]
    [InlineData("899-99-9999")]
    [InlineData("123-45-6789")]
    [InlineData("078051120")]                        // Without dashes
    [InlineData("219-09-9999")]
    public void SocialSecurityNumber_ValidValues_PassValidation(string ssn)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SocialSecurityNumber();

        ValidationResult result = validator.Validate(new TestModel(ssn));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("000-05-1120")]                      // Area 000
    [InlineData("666-05-1120")]                      // Area 666
    [InlineData("900-05-1120")]                      // Area 900-999
    [InlineData("999-05-1120")]                      // Area 999
    [InlineData("078-00-1120")]                      // Group 00
    [InlineData("078-05-0000")]                      // Serial 0000
    [InlineData("12-345-6789")]                      // Wrong format
    [InlineData("1234-56-789")]                      // Wrong format
    [InlineData("abc-de-fghi")]                      // Non-numeric
    [InlineData("078-05-112")]                       // Too short
    [InlineData("078-05-11200")]                     // Too long
    public void SocialSecurityNumber_InvalidValues_FailValidation(string? ssn)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SocialSecurityNumber();

        ValidationResult result = validator.Validate(new TestModel(ssn));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UsSsn");
    }

    // =========================================================================
    // Ein
    // =========================================================================

    [Theory]
    [InlineData("12-3456789")]                       // Brookhaven prefix
    [InlineData("20-1234567")]                       // Internet prefix
    [InlineData("80-9876543")]                       // Ogden prefix
    [InlineData("77-1234567")]                       // Philadelphia prefix
    [InlineData("123456789")]                        // Without dash
    public void Ein_ValidValues_PassValidation(string ein)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Ein();

        ValidationResult result = validator.Validate(new TestModel(ein));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("00-1234567")]                       // Invalid prefix 00
    [InlineData("07-1234567")]                       // Invalid prefix 07
    [InlineData("19-1234567")]                       // Invalid prefix 19
    [InlineData("12-345678")]                        // Too short
    [InlineData("12-34567890")]                      // Too long
    [InlineData("AB-1234567")]                       // Non-numeric prefix
    public void Ein_InvalidValues_FailValidation(string? ein)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Ein();

        ValidationResult result = validator.Validate(new TestModel(ein));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UsEin");
    }

    // =========================================================================
    // UsStateCode
    // =========================================================================

    [Theory]
    [InlineData("NY")]
    [InlineData("CA")]
    [InlineData("TX")]
    [InlineData("DC")]                               // District of Columbia
    [InlineData("PR")]                               // Puerto Rico
    [InlineData("GU")]                               // Guam
    [InlineData("AA")]                               // Armed forces Americas
    [InlineData("ny")]                               // Case-insensitive
    public void UsStateCode_ValidValues_PassValidation(string code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).UsStateCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XX")]                               // Not a state
    [InlineData("ZZ")]                               // Not a state
    [InlineData("N")]                                // Too short
    [InlineData("NYC")]                              // Too long
    [InlineData("12")]                               // Numeric
    public void UsStateCode_InvalidValues_FailValidation(string? code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).UsStateCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UsStateCode");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
