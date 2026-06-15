using Shouldly;
using Xunit;

namespace Granit.Presence.EntityFrameworkCore.Tests;

public sealed class PresenceDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_defaults_to_presence_underscore() =>
        GranitPresenceDbProperties.DbTablePrefix.ShouldBe("presence_");
}
