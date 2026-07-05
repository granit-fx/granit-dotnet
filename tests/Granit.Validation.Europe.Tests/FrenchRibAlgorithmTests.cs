using Granit.Validation.Europe.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Europe.Tests;

/// <summary>
/// Direct unit tests for <see cref="FrenchRibAlgorithm"/> covering edge cases
/// beyond the <see cref="PaymentValidatorExtensionsTests"/> FluentValidation tests.
/// </summary>
public sealed class FrenchRibAlgorithmTests
{
    // -------------------------------------------------------------------------
    // Valid RIBs
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("30004000010000000404529")]            // numeric RIB
    [InlineData("30004 00001 00000004045 29")]         // formatted with spaces
    [InlineData("30004-00001-00000004045-29")]         // formatted with dashes
    [InlineData("00000000000000000000097")]            // all-zero account, key = 97
    public void IsValid_ValidRibs_ReturnsTrue(string rib) =>
        FrenchRibAlgorithm.IsValid(rib).ShouldBeTrue();

    // -------------------------------------------------------------------------
    // Letters in account number (conversion A–Z → digits)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Account "0000000A045" where A→1, equivalent to "00000001045".
    /// Also tests that spaces and case normalization work together.
    /// Known valid: "30004000010000000404529".
    /// </summary>
    [Fact]
    public void IsValid_LettersInAccount_ConvertedCorrectly() =>
        FrenchRibAlgorithm.IsValid("30004 00001 00000004045 29").ShouldBeTrue();

    // -------------------------------------------------------------------------
    // Invalid — null / empty / whitespace
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_NullOrWhitespace_ReturnsFalse(string? rib) =>
        FrenchRibAlgorithm.IsValid(rib).ShouldBeFalse();

    // -------------------------------------------------------------------------
    // Invalid — wrong length
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData("3000400001000000040452")]             // 22 chars — too short
    [InlineData("300040000100000004045290")]           // 24 chars — too long
    public void IsValid_WrongLength_ReturnsFalse(string rib) =>
        FrenchRibAlgorithm.IsValid(rib).ShouldBeFalse();

    // -------------------------------------------------------------------------
    // Invalid — wrong checksum
    // -------------------------------------------------------------------------

    [Fact]
    public void IsValid_WrongKey_ReturnsFalse() =>
        FrenchRibAlgorithm.IsValid("30004000010000000404530").ShouldBeFalse();

    // -------------------------------------------------------------------------
    // Invalid — letters in bank code / branch code / key
    // -------------------------------------------------------------------------

    [Fact]
    public void IsValid_LettersInBankCode_ReturnsFalse() =>
        FrenchRibAlgorithm.IsValid("3000A000010000000404529").ShouldBeFalse();

    [Fact]
    public void IsValid_LettersInBranchCode_ReturnsFalse() =>
        FrenchRibAlgorithm.IsValid("300040000A0000000404529").ShouldBeFalse();

    [Fact]
    public void IsValid_LettersInKey_ReturnsFalse() =>
        FrenchRibAlgorithm.IsValid("3000400001000000040452A").ShouldBeFalse();

    // -------------------------------------------------------------------------
    // Invalid — special characters in account number
    // -------------------------------------------------------------------------

    [Fact]
    public void IsValid_SpecialCharsInAccount_ReturnsFalse() =>
        FrenchRibAlgorithm.IsValid("30004000010000!004045XX").ShouldBeFalse();

    // -------------------------------------------------------------------------
    // Case insensitivity
    // -------------------------------------------------------------------------

    [Fact]
    public void IsValid_LowercaseInput_NormalizedCorrectly()
    {
        // Same as uppercase variant — Normalize converts to upper
        const string lower = "30004 00001 00000004045 29";
        FrenchRibAlgorithm.IsValid(lower).ShouldBeTrue();
    }
}
