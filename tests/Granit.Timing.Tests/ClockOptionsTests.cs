using Granit.Timing.Options;
using Shouldly;
using Xunit;

namespace Granit.Timing.Tests;

public sealed class ClockOptionsTests
{
    [Fact]
    public void DefaultTimezone_DefaultValue_IsNull()
    {
        ClockOptions options = new();

        options.DefaultTimezone.ShouldBeNull();
    }

    [Fact]
    public void DefaultTimezone_CanBeSet()
    {
        ClockOptions options = new() { DefaultTimezone = "Europe/Brussels" };

        options.DefaultTimezone.ShouldBe("Europe/Brussels");
    }

    [Fact]
    public void DefaultTimezone_CanBeUpdated()
    {
        ClockOptions options = new() { DefaultTimezone = "America/New_York" };
        options.DefaultTimezone = "Asia/Tokyo";

        options.DefaultTimezone.ShouldBe("Asia/Tokyo");
    }

    [Fact]
    public void DefaultTimezone_CanBeSetToNull()
    {
        ClockOptions options = new() { DefaultTimezone = "UTC" };
        options.DefaultTimezone = null;

        options.DefaultTimezone.ShouldBeNull();
    }
}
