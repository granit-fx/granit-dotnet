using Granit.Validation.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class Iso4217CurrencyCodeAlgorithmTests
{
    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    [InlineData("CHF")]
    [InlineData("eur")]                                  // Lowercase
    [InlineData("  EUR  ")]                              // With whitespace
    [InlineData("ZWL")]                                  // Less common currency
    [InlineData("XAU")]                                  // Precious metal
    [InlineData("XXX")]                                  // No currency
    public void IsValid_ValidCode_ReturnsTrue(string code) => Iso4217CurrencyCodeAlgorithm.IsValid(code).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XYZ")]                                  // Not a real code
    [InlineData("AB")]                                   // Too short
    [InlineData("EURO")]                                 // Too long
    [InlineData("123")]                                  // Numeric
    [InlineData("EU")]                                   // 2 chars
    public void IsValid_InvalidCode_ReturnsFalse(string? code) => Iso4217CurrencyCodeAlgorithm.IsValid(code).ShouldBeFalse();
}
