// =============================================================================
// Tests - CanadianAddressValidatorExtensions
// =============================================================================
// CanadianPostalCode: A1A 1A1 (letter-digit-letter space digit-letter-digit)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.NorthAmerica.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.NorthAmerica.Tests;

public sealed class CanadianAddressValidatorExtensionsTests
{
    // =========================================================================
    // CanadianPostalCode
    // =========================================================================

    [Theory]
    [InlineData("K1A 0B1")]                          // Ottawa (Parliament)
    [InlineData("H3Z 2Y7")]                          // Montreal
    [InlineData("V6B 3K9")]                          // Vancouver
    [InlineData("M5V 2T6")]                          // Toronto
    [InlineData("T2P 1J9")]                          // Calgary
    [InlineData("K1A0B1")]                           // Without space
    [InlineData("k1a 0b1")]                          // Lowercase
    public void CanadianPostalCode_ValidValues_PassValidation(string postal)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).CanadianPostalCode();

        ValidationResult result = validator.Validate(new TestModel(postal));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("D1A 0B1")]                          // D not allowed anywhere
    [InlineData("F1A 0B1")]                          // F not allowed anywhere
    [InlineData("I1A 0B1")]                          // I not allowed anywhere
    [InlineData("O1A 0B1")]                          // O not allowed anywhere
    [InlineData("Q1A 0B1")]                          // Q not allowed anywhere
    [InlineData("U1A 0B1")]                          // U not allowed anywhere
    [InlineData("W1A 0B1")]                          // W not allowed as first letter
    [InlineData("Z1A 0B1")]                          // Z not allowed as first letter
    [InlineData("K1A 0B")]                           // Too short
    [InlineData("K1A 0B12")]                         // Too long
    [InlineData("11A 0B1")]                          // Starts with digit
    [InlineData("K1A-0B1")]                          // Dash instead of space
    [InlineData("12345")]                            // US ZIP
    public void CanadianPostalCode_InvalidValues_FailValidation(string? postal)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).CanadianPostalCode();

        ValidationResult result = validator.Validate(new TestModel(postal));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidCanadianPostalCode");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
