using Granit.Settings.Options;
using Shouldly;
using Xunit;

namespace Granit.Settings.Tests;

public sealed class SettingsOptionsTests
{
    [Fact]
    public void SectionName_IsSettings() => SettingsOptions.SectionName.ShouldBe("Settings");

    [Fact]
    public void CacheExpiration_DefaultIs30Minutes()
    {
        SettingsOptions options = new();

        options.CacheExpiration.ShouldBe(TimeSpan.FromMinutes(30));
    }

}
