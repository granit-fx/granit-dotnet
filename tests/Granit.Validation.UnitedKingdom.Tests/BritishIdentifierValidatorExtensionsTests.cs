// =============================================================================
// Tests - BritishIdentifierValidatorExtensions
// =============================================================================
// NationalInsuranceNumber: 2 letters + 6 digits + A-D suffix
// NhsNumber:               10 digits, MOD 11 check digit
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.UnitedKingdom.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.UnitedKingdom.Tests;

public sealed class BritishIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // NationalInsuranceNumber
    // =========================================================================

    [Theory]
    [InlineData("AB123456C")]
    [InlineData("CE123456A")]
    [InlineData("AB 12 34 56 C")]                    // With spaces
    [InlineData("AB-12-34-56-D")]                    // With dashes
    [InlineData("ab123456c")]                        // Lowercase
    [InlineData("JY987654B")]
    public void NationalInsuranceNumber_ValidValues_PassValidation(string ni)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).NationalInsuranceNumber();

        ValidationResult result = validator.Validate(new TestModel(ni));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("BG123456A")]                        // Invalid prefix BG
    [InlineData("GB123456A")]                        // Invalid prefix GB
    [InlineData("NK123456A")]                        // Invalid prefix NK
    [InlineData("KN123456A")]                        // Invalid prefix KN
    [InlineData("TN123456A")]                        // Invalid prefix TN
    [InlineData("NT123456A")]                        // Invalid prefix NT
    [InlineData("ZZ123456A")]                        // Invalid prefix ZZ
    [InlineData("DA123456A")]                        // First letter D
    [InlineData("FA123456A")]                        // First letter F
    [InlineData("IA123456A")]                        // First letter I
    [InlineData("QA123456A")]                        // First letter Q
    [InlineData("UA123456A")]                        // First letter U
    [InlineData("VA123456A")]                        // First letter V
    [InlineData("AD123456A")]                        // Second letter D
    [InlineData("AF123456A")]                        // Second letter F
    [InlineData("AI123456A")]                        // Second letter I
    [InlineData("AO123456A")]                        // Second letter O
    [InlineData("AQ123456A")]                        // Second letter Q
    [InlineData("AU123456A")]                        // Second letter U
    [InlineData("AV123456A")]                        // Second letter V
    [InlineData("AB123456E")]                        // Invalid suffix E
    [InlineData("AB12345A")]                         // Too few digits
    [InlineData("AB1234567A")]                       // Too many digits
    [InlineData("A1234567A")]                        // Only 1 prefix letter
    [InlineData("ABC123456A")]                       // 3 prefix letters
    public void NationalInsuranceNumber_InvalidValues_FailValidation(string? ni)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).NationalInsuranceNumber();

        ValidationResult result = validator.Validate(new TestModel(ni));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UkNationalInsuranceNumber");
    }

    // =========================================================================
    // NhsNumber
    // =========================================================================

    [Theory]
    [InlineData("4505577104")]                       // Valid MOD 11
    [InlineData("450 557 7104")]                     // With spaces
    [InlineData("450-557-7104")]                     // With dashes
    public void NhsNumber_ValidValues_PassValidation(string nhs)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).NhsNumber();

        ValidationResult result = validator.Validate(new TestModel(nhs));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("4505577105")]                       // Wrong check digit
    [InlineData("123456789")]                        // Too short (9 digits)
    [InlineData("12345678901")]                      // Too long (11 digits)
    [InlineData("ABCDEFGHIJ")]                       // Non-numeric
    public void NhsNumber_InvalidValues_FailValidation(string? nhs)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).NhsNumber();

        ValidationResult result = validator.Validate(new TestModel(nhs));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:UkNhsNumber");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
