using Granit.Http.Idempotency.Models;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotencyStateTests
{
    [Fact]
    public void InProgress_HasValue0() => ((byte)IdempotencyState.InProgress).ShouldBe((byte)0);

    [Fact]
    public void Completed_HasValue1() => ((byte)IdempotencyState.Completed).ShouldBe((byte)1);

    [Fact]
    public void Enum_HasExactlyTwoValues()
    {
        string[] names = Enum.GetNames<IdempotencyState>();

        names.Length.ShouldBe(2);
        names.ShouldContain("InProgress");
        names.ShouldContain("Completed");
    }
}
