using Granit.Validation.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class BicSwiftAlgorithmTests
{
    [Theory]
    [InlineData("GEBABEBB")]                             // 8-char BIC
    [InlineData("BNPAFRPP")]                             // BNP Paribas
    [InlineData("GEBABEBB36A")]                          // 11-char with branch
    [InlineData("gebabebb")]                             // Lowercase
    [InlineData("  GEBABEBB  ")]                         // With whitespace
    public void IsValid_ValidBic_ReturnsTrue(string bic) => BicSwiftAlgorithm.IsValid(bic).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("GEBA")]                                 // Too short
    [InlineData("GEBABE")]                               // 6 chars
    [InlineData("GEBABEBB36AB")]                         // 12 chars
    [InlineData("12345678")]                             // All digits
    [InlineData("GEBA1BBB")]                             // Digit in country position
    public void IsValid_InvalidBic_ReturnsFalse(string? bic) => BicSwiftAlgorithm.IsValid(bic).ShouldBeFalse();
}
