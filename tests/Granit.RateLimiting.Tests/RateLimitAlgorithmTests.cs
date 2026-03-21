using Granit.RateLimiting.Options;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class RateLimitAlgorithmTests
{
    [Fact]
    public void SlidingWindow_HasValue0() => ((byte)RateLimitAlgorithm.SlidingWindow).ShouldBe((byte)0);

    [Fact]
    public void FixedWindow_HasValue1() => ((byte)RateLimitAlgorithm.FixedWindow).ShouldBe((byte)1);

    [Fact]
    public void TokenBucket_HasValue2() => ((byte)RateLimitAlgorithm.TokenBucket).ShouldBe((byte)2);

    [Fact]
    public void Concurrency_HasValue3() => ((byte)RateLimitAlgorithm.Concurrency).ShouldBe((byte)3);

    [Fact]
    public void Enum_HasFourValues()
    {
        string[] names = Enum.GetNames<RateLimitAlgorithm>();

        names.Length.ShouldBe(4);
    }
}
