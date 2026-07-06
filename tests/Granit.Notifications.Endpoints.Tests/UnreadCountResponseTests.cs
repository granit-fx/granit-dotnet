using Granit.Notifications.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Endpoints.Tests;

public sealed class UnreadCountResponseTests
{
    [Fact]
    public void Record_Equality_SameCount_AreEqual() =>
        new UnreadCountResponse(5).ShouldBe(new UnreadCountResponse(5));

    [Fact]
    public void Record_Equality_DifferentCount_AreNotEqual() =>
        new UnreadCountResponse(5).ShouldNotBe(new UnreadCountResponse(10));
}
