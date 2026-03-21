using Granit.Validation.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class CreditCardAlgorithmTests
{
    // =========================================================================
    // Visa
    // =========================================================================

    [Theory]
    [InlineData("4111111111111111")]                     // Visa 16
    [InlineData("4012888888881881")]                     // Visa alternate
    [InlineData("4111 1111 1111 1111")]                  // With spaces
    [InlineData("4111-1111-1111-1111")]                  // With dashes
    public void IsValid_Visa_ReturnsTrue(string card) => CreditCardAlgorithm.IsValid(card).ShouldBeTrue();

    // =========================================================================
    // Mastercard
    // =========================================================================

    [Theory]
    [InlineData("5500000000000004")]                     // 51xx
    [InlineData("5200828282828210")]                     // 52xx
    [InlineData("2223000048400011")]                     // 2223 range
    public void IsValid_Mastercard_ReturnsTrue(string card) => CreditCardAlgorithm.IsValid(card).ShouldBeTrue();

    // =========================================================================
    // American Express
    // =========================================================================

    [Theory]
    [InlineData("371449635398431")]                      // 37xx, 15 digits
    [InlineData("340000000000009")]                      // 34xx, 15 digits
    public void IsValid_Amex_ReturnsTrue(string card) => CreditCardAlgorithm.IsValid(card).ShouldBeTrue();

    // =========================================================================
    // Discover
    // =========================================================================

    [Theory]
    [InlineData("6011111111111117")]                     // 6011 prefix
    [InlineData("6500000000000002")]                     // 65xx prefix
    public void IsValid_Discover_ReturnsTrue(string card) => CreditCardAlgorithm.IsValid(card).ShouldBeTrue();

    // =========================================================================
    // Diners Club
    // =========================================================================

    [Theory]
    [InlineData("30569309025904")]                       // 305x, 14 digits
    [InlineData("36110361103612")]                       // 36xx
    public void IsValid_DinersClub_ReturnsTrue(string card) => CreditCardAlgorithm.IsValid(card).ShouldBeTrue();

    // =========================================================================
    // JCB
    // =========================================================================

    [Theory]
    [InlineData("3530111333300000")]                     // 3530 prefix
    public void IsValid_Jcb_ReturnsTrue(string card) => CreditCardAlgorithm.IsValid(card).ShouldBeTrue();

    // =========================================================================
    // Maestro
    // =========================================================================

    [Theory]
    [InlineData("6759649826438453")]                     // 6759 prefix
    public void IsValid_Maestro_ReturnsTrue(string card) => CreditCardAlgorithm.IsValid(card).ShouldBeTrue();

    // =========================================================================
    // Invalid cases
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("4111111111111112")]                     // Luhn fail
    [InlineData("1234567890123456")]                     // Unknown IIN prefix
    [InlineData("12345")]                                // Way too short (<12)
    [InlineData("41111111111111111111")]                  // Too long (>19)
    [InlineData("abcdefghijklmnop")]                     // Non-numeric
    [InlineData("411111111111111X")]                     // Non-digit
    public void IsValid_InvalidCard_ReturnsFalse(string? card) => CreditCardAlgorithm.IsValid(card).ShouldBeFalse();
}
