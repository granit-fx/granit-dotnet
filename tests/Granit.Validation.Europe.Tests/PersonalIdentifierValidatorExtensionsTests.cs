// =============================================================================
// Tests - PersonalIdentifierValidatorExtensions
// =============================================================================
// FrenchNir: 15 chars, mod-97 check, 2A/2B Corse support
// BelgianEid: 12 digits, mod-97 on first 10 digits
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Europe.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

public sealed class PersonalIdentifierValidatorExtensionsTests
{
    // =========================================================================
    // FrenchNir
    // =========================================================================
    // Valid test values computed from algorithm:
    //   base = 13 digits, check = 97 - (base mod 97)
    //   "100000000000047": base=1000000000000, 10^12 mod 97=50, key=47
    //   "200000000000094": base=2000000000000, 2×50=100 mod 97=3, key=94
    //   "185072A10000146": Corse 2A → replace → base=1850719100001, mod 97=51, key=46

    [Theory]
    [InlineData("100000000000047")]        // base mod 97=50, key=47
    [InlineData("200000000000094")]        // base mod 97=3, key=94
    [InlineData("185072A10000146")]        // Corse 2A — replaced by 19 before check
    public void FrenchNir_ValidValues_PassValidation(string nir)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchNir();

        ValidationResult result = validator.Validate(new TestModel(nir));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("10000000000004")]          // 14 chars — too short
    [InlineData("1000000000000470")]        // 16 chars — too long
    [InlineData("100000000000048")]         // wrong check key (should be 47)
    [InlineData("ABCDE0000000047")]         // non-digit characters
    public void FrenchNir_InvalidValues_FailValidation(string? nir)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).FrenchNir();

        ValidationResult result = validator.Validate(new TestModel(nir));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:FrenchNir");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:FrenchNir");
    }

    // =========================================================================
    // BelgianEid
    // =========================================================================
    // Valid test values computed from algorithm:
    //   base = first 10 digits, check = 97 - (base mod 97)
    //   "123456789095": base=1234567890, mod 97=2, key=95
    //   "592-0000000-16": digits=592000000016, base=5920000000, mod 97=81, key=16

    [Theory]
    [InlineData("123456789095")]           // base=1234567890, mod 97=2, key=95
    [InlineData("592-0000000-16")]         // formatted, base=5920000000, mod 97=81, key=16
    public void BelgianEid_ValidValues_PassValidation(string eid)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianEid();

        ValidationResult result = validator.Validate(new TestModel(eid));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345678909")]             // 11 digits — too short
    [InlineData("1234567890956")]           // 13 digits — too long
    [InlineData("123456789096")]            // wrong check (should be 95)
    public void BelgianEid_InvalidValues_FailValidation(string? eid)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).BelgianEid();

        ValidationResult result = validator.Validate(new TestModel(eid));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:Format:BelgianEid");
        result.Errors[0].ErrorCode.ShouldBe("Validation:Format:BelgianEid");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
