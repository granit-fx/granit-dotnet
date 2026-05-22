// =============================================================================
// Tests - FormatValidatorExtensions
// =============================================================================
// Slug:         URL-friendly string (lowercase, digits, hyphens)
// Base64String: Standard Base64 encoding
// ColorHex:     CSS hex color (#RGB, #RGBA, #RRGGBB, #RRGGBBAA)
// =============================================================================

using FluentValidation;
using FluentValidation.Results;
using Granit.Validation.Extensions;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class FormatValidatorExtensionsTests
{
    // =========================================================================
    // Slug
    // =========================================================================

    [Theory]
    [InlineData("my-blog-post")]
    [InlineData("tenant-42")]
    [InlineData("product")]
    [InlineData("a")]
    [InlineData("abc-123-def")]
    [InlineData("123")]
    public void Slug_ValidValues_PassValidation(string slug)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Slug();

        ValidationResult result = validator.Validate(new TestModel(slug));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("My-Blog-Post")]               // Uppercase
    [InlineData("-leading-hyphen")]             // Leading hyphen
    [InlineData("trailing-hyphen-")]            // Trailing hyphen
    [InlineData("double--hyphen")]              // Consecutive hyphens
    [InlineData("has space")]                   // Space
    [InlineData("has_underscore")]              // Underscore
    [InlineData("special!char")]               // Special character
    public void Slug_InvalidValues_FailValidation(string? slug)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Slug();

        ValidationResult result = validator.Validate(new TestModel(slug));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidSlug");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidSlug");
    }

    // =========================================================================
    // Base64String
    // =========================================================================

    [Theory]
    [InlineData("SGVsbG8gV29ybGQ=")]           // "Hello World"
    [InlineData("dGVzdA==")]                    // "test"
    [InlineData("YQ==")]                        // "a"
    [InlineData("AAAA")]                        // 3 null bytes
    public void Base64String_ValidValues_PassValidation(string base64)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Base64String();

        ValidationResult result = validator.Validate(new TestModel(base64));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not base64!")]
    [InlineData("SGVsbG8gV29ybGQ")]             // Missing padding
    [InlineData("====")]                        // Only padding
    public void Base64String_InvalidValues_FailValidation(string? base64)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).Base64String();

        ValidationResult result = validator.Validate(new TestModel(base64));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidBase64String");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidBase64String");
    }

    // =========================================================================
    // ColorHex
    // =========================================================================

    [Theory]
    [InlineData("#FF5733")]                     // 6-char RGB
    [InlineData("#fff")]                        // 3-char shorthand
    [InlineData("#00FF00FF")]                   // 8-char RGBA
    [InlineData("#0af4")]                       // 4-char RGBA shorthand
    [InlineData("#000")]
    [InlineData("#FFFFFF")]
    public void ColorHex_ValidValues_PassValidation(string color)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).ColorHex();

        ValidationResult result = validator.Validate(new TestModel(color));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("FF5733")]                      // Missing #
    [InlineData("#GG5733")]                     // Non-hex character
    [InlineData("#FF")]                         // Too short (2 chars)
    [InlineData("#FF573")]                      // 5 chars (invalid length)
    [InlineData("#FF57337")]                    // 7 chars (invalid length)
    [InlineData("red")]                         // CSS color name
    public void ColorHex_InvalidValues_FailValidation(string? color)
    {
        InlineValidator<TestModel> validator = [];
        validator.RuleFor(x => x.Value).ColorHex();

        ValidationResult result = validator.Validate(new TestModel(color));

        result.IsValid.ShouldBeFalse();
        result.Errors[0].ErrorMessage.ShouldBe("Validation:InvalidColorHex");
        result.Errors[0].ErrorCode.ShouldBe("Validation:InvalidColorHex");
    }

    // -------------------------------------------------------------------------
    // Test doubles
    // -------------------------------------------------------------------------

    private sealed record TestModel(string? Value);
}
