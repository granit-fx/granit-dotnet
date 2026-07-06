using Granit.Settings.Events;
using Shouldly;
using Xunit;

namespace Granit.Settings.Tests;

public sealed class SettingChangedEventTests
{

    [Fact]
    public void Record_Equality_SameValues_AreEqual()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        SettingChangedEvent a = new("App.Theme", "G", null, "old", "new", timestamp);
        SettingChangedEvent b = new("App.Theme", "G", null, "old", "new", timestamp);

        a.ShouldBe(b);
    }

}
