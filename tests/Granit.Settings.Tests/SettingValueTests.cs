using Granit.Settings.Values;
using Shouldly;
using Xunit;

namespace Granit.Settings.Tests;

public sealed class SettingValueTests
{

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

}
