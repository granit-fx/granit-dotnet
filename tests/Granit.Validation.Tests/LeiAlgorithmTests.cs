using Granit.Validation.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class LeiAlgorithmTests
{
    [Theory]
    [InlineData("7ZW8QJWVPR4P1J1KQY45")]               // Deutsche Bank
    [InlineData("529900T8BM49AURSDO55")]                 // Sample LEI
    [InlineData("529900t8bm49aursdo55")]                 // Lowercase
    [InlineData("  7ZW8QJWVPR4P1J1KQY45  ")]            // With whitespace
    public void IsValid_ValidLei_ReturnsTrue(string lei) => LeiAlgorithm.IsValid(lei).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("529900T8BM49AURSDO5")]                  // 19 chars
    [InlineData("529900T8BM49AURSDO555")]                // 21 chars
    [InlineData("529900T8BM49AURSDO56")]                 // Wrong check digits
    [InlineData("5299-0T8BM49AURSDO55")]                 // Special characters
    [InlineData("529900T8BM49AURSDOAA")]                 // Check digits not numeric
    public void IsValid_InvalidLei_ReturnsFalse(string? lei) => LeiAlgorithm.IsValid(lei).ShouldBeFalse();
}
