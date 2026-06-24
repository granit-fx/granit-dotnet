// =============================================================================
// Tests - BritishAddressValidatorExtensions
// =============================================================================
// UkPostcode: Standard UK postcode formats (A9 9AA through AA9A 9AA)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.UnitedKingdom.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.UnitedKingdom.Tests;

public sealed class BritishAddressValidatorExtensionsTests
{
    // =========================================================================
    // UkPostcode
    // =========================================================================

    [Theory]
    [InlineData("SW1A 1AA")]                         // Buckingham Palace
    [InlineData("EC1A 1BB")]                         // London (AA9A 9AA)
    [InlineData("W1A 0AX")]                          // BBC (A9A 9AA)
    [InlineData("M1 1AE")]                           // Manchester (A9 9AA)
    [InlineData("B33 8TH")]                          // Birmingham (A99 9AA)
    [InlineData("CR2 6XH")]                          // Croydon (AA9 9AA)
    [InlineData("DN55 1PT")]                         // Doncaster (AA99 9AA)
    [InlineData("sw1a 1aa")]                         // Lowercase
    [InlineData("SW1A1AA")]                          // Without space
    public void UkPostcode_ValidValues_PassValidation(string postcode)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).UkPostcode();

        ValidationResult result = validator.Validate(new TestModel(postcode));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1SW 1AA")]                          // Starts with digit
    [InlineData("SW1")]                              // Too short (outward only)
    [InlineData("SW1A 1A")]                          // Incomplete inward
    [InlineData("SW1A 11A")]                         // Two digits in inward
    [InlineData("SW1A AAA")]                         // No digit in inward
    [InlineData("12345")]                            // US ZIP
    [InlineData("A1A 1A1")]                          // Canadian postal code
    public void UkPostcode_InvalidValues_FailValidation(string? postcode)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).UkPostcode();

        ValidationResult result = validator.Validate(new TestModel(postcode));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UkPostcode");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
