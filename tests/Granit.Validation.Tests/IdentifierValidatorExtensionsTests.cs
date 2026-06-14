// =============================================================================
// Tests - International identifier validators
// =============================================================================
// Verifies each identifier validator with real valid/invalid values.
// Error codes follow the convention Validation:* (WithMessage = WithErrorCode).
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class IdentifierValidatorExtensionsTests
{
    // =========================================================================
    // E164Phone
    // =========================================================================

    [Theory]
    [InlineData("+32475123456")]        // Belgian mobile
    [InlineData("+33612345678")]        // French mobile
    [InlineData("+14155552671")]        // US
    [InlineData("+442071234567")]       // UK
    public void E164Phone_ValidValues_PassValidation(string phone)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).E164Phone();

        ValidationResult result = validator.Validate(new TestModel(phone));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0475123456")]          // missing +
    [InlineData("+32")]                 // too short
    [InlineData("+3247512345678901")]   // too long (>15 digits)
    [InlineData("+0475123456")]         // leading 0 after +
    [InlineData("+32 475 12 34 56")]    // spaces not allowed
    public void E164Phone_InvalidValues_FailValidation(string? phone)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).E164Phone();

        ValidationResult result = validator.Validate(new TestModel(phone));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:E164Phone");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
