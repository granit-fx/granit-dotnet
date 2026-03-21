using Granit.RateLimiting.Options;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class CounterStoreFailureBehaviorTests
{
    [Fact]
    public void Allow_HasValue0() => ((byte)CounterStoreFailureBehavior.Allow).ShouldBe((byte)0);

    [Fact]
    public void Deny_HasValue1() => ((byte)CounterStoreFailureBehavior.Deny).ShouldBe((byte)1);

    [Fact]
    public void Enum_HasTwoValues()
    {
        string[] names = Enum.GetNames<CounterStoreFailureBehavior>();

        names.Length.ShouldBe(2);
    }
}
