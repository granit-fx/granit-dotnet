// =============================================================================
// Tests - FinancialValidatorExtensions
// =============================================================================
// CreditCard: ISO/IEC 7812-1, Luhn + IIN prefix (Visa, MC, Amex, Discover, etc.)
// LEI:        ISO 17442, 20-char alphanumeric, MOD 97-10 check digits
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class FinancialValidatorExtensionsTests
{
    // =========================================================================
    // CreditCard
    // =========================================================================

    [Theory]
    [InlineData("4111111111111111")]           // Visa 16 digits
    [InlineData("4111 1111 1111 1111")]        // Visa with spaces
    [InlineData("4111-1111-1111-1111")]        // Visa with dashes
    [InlineData("4012888888881881")]           // Visa alternate
    [InlineData("5500000000000004")]           // Mastercard (51xx)
    [InlineData("5200828282828210")]           // Mastercard (52xx)
    [InlineData("2223000048400011")]           // Mastercard (2223)
    [InlineData("371449635398431")]            // American Express (37xx)
    [InlineData("340000000000009")]            // American Express (34xx)
    [InlineData("6011111111111117")]           // Discover (6011)
    [InlineData("6500000000000002")]           // Discover (65xx)
    [InlineData("3530111333300000")]           // JCB (3530)
    [InlineData("30569309025904")]             // Diners Club (305x)
    [InlineData("36110361103612")]             // Diners Club (36xx)
    [InlineData("6759649826438453")]           // Maestro (6759)
    public void CreditCard_ValidValues_PassValidation(string card)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).CreditCardNumber();

        ValidationResult result = validator.Validate(new TestModel(card));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("4111111111111112")]           // Luhn fail
    [InlineData("1234567890123456")]           // Unknown IIN prefix
    [InlineData("411111111111")]               // Too short for Visa (11 digits)
    [InlineData("12345")]                      // Way too short
    [InlineData("abcdefghijklmnop")]           // Non-numeric
    [InlineData("411111111111111X")]           // Non-digit character
    public void CreditCard_InvalidValues_FailValidation(string? card)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).CreditCardNumber();

        ValidationResult result = validator.Validate(new TestModel(card));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:CreditCard");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:CreditCard");
    }

    // =========================================================================
    // LEI
    // =========================================================================

    [Theory]
    [InlineData("7ZW8QJWVPR4P1J1KQY45")]     // Deutsche Bank
    [InlineData("529900T8BM49AURSDO55")]       // Sample LEI
    [InlineData("529900t8bm49aursdo55")]       // lowercase — normalised
    public void Lei_ValidValues_PassValidation(string lei)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Lei();

        ValidationResult result = validator.Validate(new TestModel(lei));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("529900T8BM49AURSDO5")]        // 19 chars — too short
    [InlineData("529900T8BM49AURSDO555")]      // 21 chars — too long
    [InlineData("529900T8BM49AURSDO56")]       // Wrong check digits (mod97 ≠ 1)
    [InlineData("529900T8BM49AURSD055")]       // Wrong check digits (different arrangement)
    [InlineData("5299-0T8BM49AURSDO55")]       // Special characters
    public void Lei_InvalidValues_FailValidation(string? lei)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Lei();

        ValidationResult result = validator.Validate(new TestModel(lei));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:Lei");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:Lei");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
