// =============================================================================
// Tests - AmericanAddressValidatorExtensions
// =============================================================================
// UsZipCode:        5-digit or ZIP+4 (XXXXX or XXXXX-XXXX)
// NanpPhoneNumber:  NXX-NXX-XXXX (N = 2-9)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.NorthAmerica.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.NorthAmerica.Tests;

public sealed class AmericanAddressValidatorExtensionsTests
{
    // =========================================================================
    // UsZipCode
    // =========================================================================

    [Theory]
    [InlineData("10001")]                            // NYC
    [InlineData("90210")]                            // Beverly Hills
    [InlineData("00501")]                            // IRS Holtsville
    [InlineData("99950")]                            // Ketchikan AK
    [InlineData("10001-1234")]                       // ZIP+4
    [InlineData("00000")]                            // Edge: all zeros
    public void UsZipCode_ValidValues_PassValidation(string zip)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).UsZipCode();

        ValidationResult result = validator.Validate(new TestModel(zip));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]                             // Too short
    [InlineData("123456")]                           // Too long (6 digits)
    [InlineData("ABCDE")]                            // Letters
    [InlineData("10001-123")]                        // ZIP+3 (not +4)
    [InlineData("10001-12345")]                      // ZIP+5
    [InlineData("10001 1234")]                       // Space instead of dash
    public void UsZipCode_InvalidValues_FailValidation(string? zip)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).UsZipCode();

        ValidationResult result = validator.Validate(new TestModel(zip));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UsZipCode");
    }

    // =========================================================================
    // NanpPhoneNumber
    // =========================================================================

    [Theory]
    [InlineData("212-555-1234")]                     // NYC
    [InlineData("(415) 555-2671")]                   // SF with parens
    [InlineData("415.555.2671")]                     // Dot-separated
    [InlineData("4155552671")]                       // No formatting
    [InlineData("1-212-555-1234")]                   // With country code
    [InlineData("+1 212 555 1234")]                  // International format
    [InlineData("+12125551234")]                     // Compact international
    [InlineData("800-555-1212")]                     // Toll-free
    public void NanpPhoneNumber_ValidValues_PassValidation(string phone)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).NanpPhoneNumber();

        ValidationResult result = validator.Validate(new TestModel(phone));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("012-555-1234")]                     // Area starts with 0
    [InlineData("112-555-1234")]                     // Area starts with 1
    [InlineData("212-055-1234")]                     // Exchange starts with 0
    [InlineData("212-155-1234")]                     // Exchange starts with 1
    [InlineData("555-1234")]                         // 7 digits only (no area)
    [InlineData("+2 212 555 1234")]                  // Wrong country code
    [InlineData("212-555-123")]                      // Too short
    [InlineData("212-555-12345")]                    // Too long
    public void NanpPhoneNumber_InvalidValues_FailValidation(string? phone)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).NanpPhoneNumber();

        ValidationResult result = validator.Validate(new TestModel(phone));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:NanpPhoneNumber");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
