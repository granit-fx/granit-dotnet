using Shouldly;
using Xunit;

namespace Granit.Localization.Tests;

public sealed class LanguageInfoTests
{
    [Fact]
    public void Constructor_WithoutFlagIcon_DefaultsToNull()
    {
        LanguageInfo lang = new("en", "English");

        lang.CultureName.ShouldBe("en");
        lang.DisplayName.ShouldBe("English");
        lang.FlagIcon.ShouldBeNull();
        lang.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_RegionalCulture_SetsCorrectly()
    {
        LanguageInfo lang = new("fr-CA", "Français (Canada)", "ca");

        lang.CultureName.ShouldBe("fr-CA");
        lang.DisplayName.ShouldBe("Français (Canada)");
        lang.FlagIcon.ShouldBe("ca");
        lang.IsDefault.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_WithIsDefault_SetsProperty()
    {
        LanguageInfo lang = new("en", "English", "gb", isDefault: true);

        lang.IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsEverything()
    {
        LanguageInfo lang = new("en", "English", "gb", isDefault: true);

        lang.CultureName.ShouldBe("en");
        lang.DisplayName.ShouldBe("English");
        lang.FlagIcon.ShouldBe("gb");
        lang.IsDefault.ShouldBeTrue();
    }
}
