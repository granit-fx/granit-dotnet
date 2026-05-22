// =============================================================================
// Tests - StandardValidatorExtensions
// =============================================================================
// Iso4217CurrencyCode: ISO 4217 alphabetic currency codes
// Iso8601Duration:     ISO 8601 duration format (PnYnMnDTnHnMnS)
// Uuid:                RFC 9562 canonical format (8-4-4-4-12)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class StandardValidatorExtensionsTests
{
    // =========================================================================
    // Iso4217CurrencyCode
    // =========================================================================

    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("CHF")]
    [InlineData("eur")]                        // lowercase — normalised
    [InlineData("  EUR  ")]                    // trimmed
    public void Iso4217CurrencyCode_ValidValues_PassValidation(string code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Iso4217CurrencyCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XYZ")]                        // Not a real ISO 4217 code
    [InlineData("AB")]                         // Too short
    [InlineData("EURO")]                       // Too long
    [InlineData("123")]                        // Numeric
    public void Iso4217CurrencyCode_InvalidValues_FailValidation(string? code)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Iso4217CurrencyCode();

        ValidationResult result = validator.Validate(new TestModel(code));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidIso4217CurrencyCode");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidIso4217CurrencyCode");
    }

    // =========================================================================
    // Iso8601Duration
    // =========================================================================

    [Theory]
    [InlineData("P1Y")]                        // 1 year
    [InlineData("P1M")]                        // 1 month
    [InlineData("P1D")]                        // 1 day
    [InlineData("PT1H")]                       // 1 hour
    [InlineData("PT1M")]                       // 1 minute
    [InlineData("PT1S")]                       // 1 second
    [InlineData("PT1.5S")]                     // 1.5 seconds (fractional)
    [InlineData("P1Y2M3DT4H5M6S")]            // Full duration
    [InlineData("PT30M")]                      // 30 minutes
    [InlineData("P365D")]                      // 365 days
    [InlineData("P1Y6M")]                      // 1 year 6 months
    public void Iso8601Duration_ValidValues_PassValidation(string duration)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Iso8601Duration();

        ValidationResult result = validator.Validate(new TestModel(duration));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("P")]                          // Missing components
    [InlineData("PT")]                         // Missing time components
    [InlineData("1Y2M3D")]                     // Missing P prefix
    [InlineData("P1H")]                        // H without T separator
    [InlineData("PTS")]                        // Missing number before S
    [InlineData("P-1Y")]                       // Negative not allowed
    [InlineData("hello")]
    public void Iso8601Duration_InvalidValues_FailValidation(string? duration)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Iso8601Duration();

        ValidationResult result = validator.Validate(new TestModel(duration));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidIso8601Duration");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidIso8601Duration");
    }

    // =========================================================================
    // Uuid
    // =========================================================================

    [Theory]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("00000000-0000-0000-0000-000000000000")]  // Nil UUID
    [InlineData("A550E840-E29B-41D4-A716-446655440000")]  // Uppercase
    public void Uuid_ValidValues_PassValidation(string uuid)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Uuid();

        ValidationResult result = validator.Validate(new TestModel(uuid));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("550e8400e29b41d4a716446655440000")]       // No dashes
    [InlineData("550e8400-e29b-41d4-a716")]                // Too short
    [InlineData("550e8400-e29b-41d4-a716-44665544000G")]   // Non-hex char
    [InlineData("{550e8400-e29b-41d4-a716-446655440000}")]  // Braces
    public void Uuid_InvalidValues_FailValidation(string? uuid)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Uuid();

        ValidationResult result = validator.Validate(new TestModel(uuid));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidUuid");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidUuid");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
