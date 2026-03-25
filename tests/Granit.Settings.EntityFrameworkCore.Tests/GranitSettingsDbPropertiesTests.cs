using Shouldly;
using Xunit;

namespace Granit.Settings.EntityFrameworkCore.Tests;

public sealed class GranitSettingsDbPropertiesTests
{
    [Fact]
    public void DbTablePrefix_Default_IsSettings() => GranitSettingsDbProperties.DbTablePrefix.ShouldBe("settings_");

    [Fact]
    public void DbSchema_Default_IsNull() => GranitSettingsDbProperties.DbSchema.ShouldBeNull();
}
