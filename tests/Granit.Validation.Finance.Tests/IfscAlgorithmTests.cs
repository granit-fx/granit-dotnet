using Granit.Validation.Finance.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Finance.Tests;

public sealed class IfscAlgorithmTests
{
    [Theory]
    [InlineData("SBIN0001234")]
    [InlineData("HDFC0CAGSBK")]
    [InlineData("sbin0001234")] // lowercase normalised
    public void IsValid_Valid_ReturnsTrue(string ifsc) => IfscAlgorithm.IsValid(ifsc).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SBIN1001234")] // 5th char not 0
    [InlineData("SBI0001234")]  // too short
    [InlineData("SBIN0001234X")] // too long
    public void IsValid_Invalid_ReturnsFalse(string? ifsc) => IfscAlgorithm.IsValid(ifsc).ShouldBeFalse();
}
