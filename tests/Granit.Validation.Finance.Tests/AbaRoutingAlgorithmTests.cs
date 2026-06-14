using Granit.Validation.Finance.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Finance.Tests;

public sealed class AbaRoutingAlgorithmTests
{
    [Theory]
    [InlineData("021000021")]
    [InlineData("011000015")]
    [InlineData("121000248")]
    public void IsValid_Valid_ReturnsTrue(string aba) => AbaRoutingAlgorithm.IsValid(aba).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("021000022")] // bad checksum
    [InlineData("12345678")]  // 8 digits
    [InlineData("0210000210")] // 10 digits
    [InlineData("02100002A")] // non-digit
    public void IsValid_Invalid_ReturnsFalse(string? aba) => AbaRoutingAlgorithm.IsValid(aba).ShouldBeFalse();
}
