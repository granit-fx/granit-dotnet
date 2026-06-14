using Granit.Validation.Finance.Internal;
using Shouldly;
using Xunit;

namespace Granit.Validation.Finance.Tests;

public sealed class CanadianRoutingAlgorithmTests
{
    [Theory]
    [InlineData("00012345")]
    [InlineData("12345-678")]
    [InlineData("000123456")] // 9-digit electronic form
    public void IsValid_Valid_ReturnsTrue(string routing) => CanadianRoutingAlgorithm.IsValid(routing).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]  // 7 digits
    [InlineData("abcdefgh")]
    public void IsValid_Invalid_ReturnsFalse(string? routing) => CanadianRoutingAlgorithm.IsValid(routing).ShouldBeFalse();
}
