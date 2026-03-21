using Shouldly;
using Xunit;

namespace Granit.Authentication.ApiKeys.Tests;

public sealed class CacheBehaviorTests
{
    [Theory]
    [InlineData(CacheBehavior.Normal, 0)]
    [InlineData(CacheBehavior.NoCache, 1)]
    public void CacheBehavior_HasExpectedIntValue(CacheBehavior behavior, int expectedValue) =>
        ((int)behavior).ShouldBe(expectedValue);

    [Fact]
    public void CacheBehavior_HasTwoValues() =>
        Enum.GetValues<CacheBehavior>().Length.ShouldBe(2);
}
