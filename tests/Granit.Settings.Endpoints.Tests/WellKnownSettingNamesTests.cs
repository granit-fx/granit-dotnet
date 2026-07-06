using Granit.Settings.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Settings.Endpoints.Tests;

public sealed class WellKnownSettingNamesTests
{
    [Fact]
    public void PreferredCulture_HasExpectedValue() => WellKnownSettingNames.PreferredCulture.ShouldBe("Granit.Localization.PreferredCulture");

    [Fact]
    public void PreferredTimezone_HasExpectedValue() => WellKnownSettingNames.PreferredTimezone.ShouldBe("Granit.Timing.PreferredTimezone");

    [Fact]
    public void PreferredFirstDayOfWeek_HasExpectedValue() => WellKnownSettingNames.PreferredFirstDayOfWeek.ShouldBe("Granit.Timing.PreferredFirstDayOfWeek");

}
