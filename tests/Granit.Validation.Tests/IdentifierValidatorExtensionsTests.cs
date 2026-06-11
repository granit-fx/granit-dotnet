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

    // =========================================================================
    // Iban
    // =========================================================================

    [Theory]
    [InlineData("BE68539007547034")]             // Belgian IBAN
    [InlineData("BE68 5390 0754 7034")]          // with spaces
    [InlineData("FR7630006000011234567890189")]  // French IBAN
    [InlineData("DE89370400440532013000")]       // German IBAN
    [InlineData("GB29NWBK60161331926819")]       // UK IBAN
    public void Iban_ValidValues_PassValidation(string iban)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Iban();

        ValidationResult result = validator.Validate(new TestModel(iban));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BE68539007547035")]         // wrong check digit
    [InlineData("XX00123456789012345")]      // invalid country code (passes format, fails MOD-97)
    [InlineData("BE685390075470")]           // too short
    [InlineData("123456789")]               // no country code
    public void Iban_InvalidValues_FailValidation(string? iban)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Iban();

        ValidationResult result = validator.Validate(new TestModel(iban));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:Iban");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
