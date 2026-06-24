// =============================================================================
// Tests - ProfessionalRegistryValidatorExtensions
// =============================================================================
// BelgianInami: 11 digits (formatted XXXXXX/XXX-XX), check = 97 − (first 9 mod 97)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class ProfessionalRegistryValidatorExtensionsTests
{
    // =========================================================================
    // BelgianInami
    // =========================================================================
    // Valid test values computed from algorithm:
    //   check = 97 − (first 9 digits mod 97)
    //   "12345678958": base=123456789, mod 97=39, key=58
    //   "00000000097": base=000000000=0, mod 97=0, key=97

    [Theory]
    [InlineData("12345678958")]            // base=123456789, mod97=39, key=58
    [InlineData("00000000097")]            // base=0, mod97=0, key=97
    [InlineData("123456/789-58")]          // formatted XXXXXX/XXX-XX
    public void BelgianInami_ValidValues_PassValidation(string inami)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianInami();

        ValidationResult result = validator.Validate(new TestModel(inami));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567895")]             // 10 digits — too short
    [InlineData("123456789580")]           // 12 digits — too long
    [InlineData("12345678959")]            // wrong check (should be 58)
    public void BelgianInami_InvalidValues_FailValidation(string? inami)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianInami();

        ValidationResult result = validator.Validate(new TestModel(inami));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:BelgianInami");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:BelgianInami");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
