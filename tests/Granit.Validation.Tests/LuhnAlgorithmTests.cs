using Granit.Validation.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class LuhnAlgorithmTests
{
    // =========================================================================
    // IsValid
    // =========================================================================

    [Theory]
    [InlineData("0")]                                    // Single zero digit
    [InlineData("18")]                                   // Simple 2-digit
    [InlineData("79927398713")]                          // ISO example
    [InlineData("4111111111111111")]                     // Visa
    public void IsValid_ValidLuhnSequence_ReturnsTrue(string digits)
    {
        bool result = LuhnAlgorithm.IsValid(digits);

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("1")]                                    // Single non-zero digit
    [InlineData("79927398714")]                          // Modified ISO example
    [InlineData("4111111111111112")]                     // Invalid Visa
    public void IsValid_InvalidLuhnSequence_ReturnsFalse(string digits)
    {
        bool result = LuhnAlgorithm.IsValid(digits);

        result.ShouldBeFalse();
    }

    // =========================================================================
    // IsValidFixedLength
    // =========================================================================

    [Theory]
    [InlineData("79927398713", 11)]
    [InlineData("4111111111111111", 16)]
    public void IsValidFixedLength_ValidInput_ReturnsTrue(string value, int length)
    {
        bool result = LuhnAlgorithm.IsValidFixedLength(value, length);

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null, 9)]
    [InlineData("", 9)]
    [InlineData("   ", 9)]
    public void IsValidFixedLength_NullOrWhitespace_ReturnsFalse(string? value, int length)
    {
        bool result = LuhnAlgorithm.IsValidFixedLength(value, length);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValidFixedLength_WrongLength_ReturnsFalse()
    {
        bool result = LuhnAlgorithm.IsValidFixedLength("79927398713", 10);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValidFixedLength_NonDigitCharacters_ReturnsFalse()
    {
        bool result = LuhnAlgorithm.IsValidFixedLength("7992739871A", 11);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValidFixedLength_InvalidLuhn_ReturnsFalse()
    {
        bool result = LuhnAlgorithm.IsValidFixedLength("79927398714", 11);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsValidFixedLength_WithLeadingTrailingWhitespace_TrimsAndValidates()
    {
        // "79927398713" padded with spaces should be trimmed to 11 chars
        bool result = LuhnAlgorithm.IsValidFixedLength("  79927398713  ", 11);

        result.ShouldBeTrue();
    }
}
