using Granit.Domain;
using Granit.Settings.Domain;
using Shouldly;
using Xunit;

namespace Granit.Settings.EntityFrameworkCore.Tests;

public sealed class SettingRecordTests
{
    [Fact]
    public void DefaultProperties_AreEmptyStrings()
    {
        SettingRecord record = new();

        record.Name.ShouldBe(string.Empty);
        record.ProviderName.ShouldBe(string.Empty);
    }

    [Fact]
    public void ProviderKey_IsNull_ByDefault()
    {
        SettingRecord record = new();

        record.ProviderKey.ShouldBeNull();
    }

    [Fact]
    public void Value_IsNull_ByDefault()
    {
        SettingRecord record = new();

        record.Value.ShouldBeNull();
    }

    [Fact]
    public void InheritsFrom_AuditedEntity()
    {
        SettingRecord record = new();

        record.ShouldBeAssignableTo<AuditedEntity>();
    }

    [Fact]
    public void Implements_IEmitEntityLifecycleEvents()
    {
        SettingRecord record = new();

        record.ShouldBeAssignableTo<IEmitEntityLifecycleEvents>();
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        SettingRecord record = new()
        {
            Name = "App.Theme",
            ProviderName = "G",
            ProviderKey = null,
            Value = "dark",
        };

        record.Name.ShouldBe("App.Theme");
        record.ProviderName.ShouldBe("G");
        record.ProviderKey.ShouldBeNull();
        record.Value.ShouldBe("dark");
    }

    [Fact]
    public void Properties_WithProviderKey_CanBeSet()
    {
        SettingRecord record = new()
        {
            Name = "App.Theme",
            ProviderName = "T",
            ProviderKey = "tenant-42",
            Value = "blue",
        };

        record.ProviderName.ShouldBe("T");
        record.ProviderKey.ShouldBe("tenant-42");
    }
}
