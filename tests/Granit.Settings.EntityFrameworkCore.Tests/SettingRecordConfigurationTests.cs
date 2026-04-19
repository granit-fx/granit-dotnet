// =============================================================================
// Tests - SettingRecordConfiguration
// =============================================================================
// Verifies table mapping, column constraints, and unique index definition.
// =============================================================================

using Granit.Settings.Domain;
using Granit.Settings.EntityFrameworkCore.Extensions;
using Granit.Settings.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Settings.EntityFrameworkCore.Tests;

public sealed class SettingRecordConfigurationTests
{
    private sealed class TestSettingsDbContext(DbContextOptions<TestSettingsDbContext> options)
        : DbContext(options), ISettingsDbContext
    {
        public DbSet<SettingRecord> SettingRecords { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureSettingsModule();
        }
    }

    private static IModel BuildModel()
    {
        DbContextOptions<TestSettingsDbContext> options =
            new DbContextOptionsBuilder<TestSettingsDbContext>()
                .UseInMemoryDatabase("schema-test")
                .Options;
        using TestSettingsDbContext context = new(options);
        return context.Model;
    }

    [Fact]
    public void SettingRecord_MapsTo_CoreSettingRecordsTable()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(SettingRecord))!;

        entity.GetTableName().ShouldBe("settings_setting_records");
    }

    [Fact]
    public void SettingRecord_HasUniqueIndex_OnNameProviderNameProviderKey()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(SettingRecord))!;

        IIndex? uniqueIndex = entity.GetIndexes()
            .FirstOrDefault(i => i.IsUnique &&
                i.Properties.Select(p => p.Name)
                    .SequenceEqual(["Name", "ProviderName", "ProviderKey"]));

        uniqueIndex.ShouldNotBeNull("a unique index on (Name, ProviderName, ProviderKey) is required to prevent duplicate records");
    }

    [Fact]
    public void SettingRecord_HasPrimaryKey_OnId()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(SettingRecord))!;

        IKey pk = entity.FindPrimaryKey()!;
        pk.Properties.ShouldContain(p => p.Name == "Id");
    }

    [Fact]
    public void SettingRecord_Name_HasMaxLength256()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(SettingRecord))!;

        IProperty name = entity.FindProperty(nameof(SettingRecord.Name))!;
        name.GetMaxLength().ShouldBe(256);
    }

    [Fact]
    public void SettingRecord_ProviderName_HasMaxLength4()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(SettingRecord))!;

        IProperty providerName = entity.FindProperty(nameof(SettingRecord.ProviderName))!;
        providerName.GetMaxLength().ShouldBe(4);
    }

    [Fact]
    public void SettingRecord_ProviderKey_IsNullable()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(SettingRecord))!;

        IProperty providerKey = entity.FindProperty(nameof(SettingRecord.ProviderKey))!;
        providerKey.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void SettingRecord_Value_IsNullable()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(SettingRecord))!;

        IProperty value = entity.FindProperty(nameof(SettingRecord.Value))!;
        value.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void SettingRecord_Value_HasMaxLength4000()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(SettingRecord))!;

        IProperty value = entity.FindProperty(nameof(SettingRecord.Value))!;
        value.GetMaxLength().ShouldBe(4000);
    }
}
