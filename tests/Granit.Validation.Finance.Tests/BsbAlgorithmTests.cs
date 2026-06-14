using Granit.Validation.Finance.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Finance.Tests;

public sealed class BsbAlgorithmTests
{
    [Theory]
    [InlineData("082902")]
    [InlineData("082-902")]
    [InlineData("032 002")]
    public void IsValid_Valid_ReturnsTrue(string bsb) => BsbAlgorithm.IsValid(bsb).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]   // 5 digits
    [InlineData("1234567")] // 7 digits
    [InlineData("abcdef")]
    public void IsValid_Invalid_ReturnsFalse(string? bsb) => BsbAlgorithm.IsValid(bsb).ShouldBeFalse();
}
