using Granit.Settings.Values;
using Shouldly;
using Xunit;

namespace Granit.Settings.Tests;

public sealed class SettingValueTests
{
    [Fact]
    public void Record_Properties_AreAccessible()
    {
        SettingValue sv = new("App.Theme", "G", null, "dark");

        sv.Name.ShouldBe("App.Theme");
        sv.ProviderName.ShouldBe("G");
        sv.ProviderKey.ShouldBeNull();
        sv.Value.ShouldBe("dark");
    }

    [Fact]
    public void Record_Equality_SameValues_AreEqual()
    {
        SettingValue a = new("App.Theme", "G", null, "dark");
        SettingValue b = new("App.Theme", "G", null, "dark");

        a.ShouldBe(b);
        (a == b).ShouldBeTrue();
    }

    [Fact]
    public void Record_Equality_DifferentValue_AreNotEqual()
    {
        SettingValue a = new("App.Theme", "G", null, "dark");
        SettingValue b = new("App.Theme", "G", null, "light");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Record_Equality_DifferentProvider_AreNotEqual()
    {
        SettingValue a = new("App.Theme", "G", null, "dark");
        SettingValue b = new("App.Theme", "T", "tenant-1", "dark");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Record_WithNullValue_IsValid()
    {
        SettingValue sv = new("App.Theme", "G", null, null);

        sv.Value.ShouldBeNull();
    }

    [Fact]
    public void Record_WithProviderKey_IsValid()
    {
        SettingValue sv = new("App.Theme", "U", "user-42", "dark");

        sv.ProviderKey.ShouldBe("user-42");
    }
}
