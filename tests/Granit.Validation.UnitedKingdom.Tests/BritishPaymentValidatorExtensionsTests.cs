// =============================================================================
// Tests - BritishPaymentValidatorExtensions
// =============================================================================
// SortCode: 6 digits (XX-XX-XX format)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.UnitedKingdom.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.UnitedKingdom.Tests;

public sealed class BritishPaymentValidatorExtensionsTests
{
    // =========================================================================
    // SortCode
    // =========================================================================

    [Theory]
    [InlineData("20-00-00")]                         // Barclays
    [InlineData("40-47-84")]                         // HSBC
    [InlineData("200000")]                           // Without dashes
    [InlineData("60 83 71")]                         // With spaces
    public void SortCode_ValidValues_PassValidation(string sortCode)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SortCode();

        ValidationResult result = validator.Validate(new TestModel(sortCode));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]                            // Too short
    [InlineData("1234567")]                          // Too long
    [InlineData("ABCDEF")]                           // Non-numeric
    [InlineData("12-34-5A")]                         // Contains letter
    public void SortCode_InvalidValues_FailValidation(string? sortCode)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).SortCode();

        ValidationResult result = validator.Validate(new TestModel(sortCode));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UkSortCode");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
