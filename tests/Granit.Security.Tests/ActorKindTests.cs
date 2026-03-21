using Shouldly;
using Xunit;

namespace Granit.Security.Tests;

public sealed class ActorKindTests
{
    [Fact]
    public void ActorKind_HasExpectedValues() =>
        Enum.GetNames<ActorKind>().ShouldBe(["User", "ExternalSystem", "System"]);

    [Fact]
    public void User_IsDefault() => default(ActorKind).ShouldBe(ActorKind.User);

    [Fact]
    public void ActorKind_HasExactlyThreeMembers() =>
        Enum.GetValues<ActorKind>().Length.ShouldBe(3);

    [Theory]
    [InlineData(ActorKind.User, 0)]
    [InlineData(ActorKind.ExternalSystem, 1)]
    [InlineData(ActorKind.System, 2)]
    public void ActorKind_HasExpectedNumericValue(ActorKind kind, int expected) =>
        ((int)kind).ShouldBe(expected);
}
