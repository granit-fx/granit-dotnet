using Granit.Settings.Events;
using Shouldly;
using Xunit;

namespace Granit.Settings.Tests;

public sealed class SettingChangedEventTests
{
    [Fact]
    public void Record_Properties_AreAccessible()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        SettingChangedEvent evt = new(
            "App.Theme", "G", null, "old", "new", timestamp);

        evt.SettingName.ShouldBe("App.Theme");
        evt.ProviderName.ShouldBe("G");
        evt.ProviderKey.ShouldBeNull();
        evt.OldValue.ShouldBe("old");
        evt.NewValue.ShouldBe("new");
        evt.Timestamp.ShouldBe(timestamp);
    }

    [Fact]
    public void Record_Equality_SameValues_AreEqual()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        SettingChangedEvent a = new("App.Theme", "G", null, "old", "new", timestamp);
        SettingChangedEvent b = new("App.Theme", "G", null, "old", "new", timestamp);

        a.ShouldBe(b);
    }

    [Fact]
    public void Record_ForCreation_HasNullOldValue()
    {
        SettingChangedEvent evt = new(
            "App.Theme", "G", null, null, "dark", DateTimeOffset.UtcNow);

        evt.OldValue.ShouldBeNull();
        evt.NewValue.ShouldBe("dark");
    }

    [Fact]
    public void Record_ForDeletion_HasNullNewValue()
    {
        SettingChangedEvent evt = new(
            "App.Theme", "G", null, "dark", null, DateTimeOffset.UtcNow);

        evt.OldValue.ShouldBe("dark");
        evt.NewValue.ShouldBeNull();
    }

    [Fact]
    public void Record_WithProviderKey_IsValid()
    {
        SettingChangedEvent evt = new(
            "App.Theme", "T", "tenant-42", "old", "new", DateTimeOffset.UtcNow);

        evt.ProviderKey.ShouldBe("tenant-42");
    }
}
