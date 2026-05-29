using Granit.Settings.Domain;
using Granit.Settings.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Settings.EntityFrameworkCore.Tests;

public sealed class ModelBuilderExtensionsTests
{
    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        public DbSet<SettingRecord> SettingRecords { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureSettingsModule();
        }
    }

    [Fact]
    public void ConfigureSettingsModule_AppliesSettingRecordConfiguration()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("model-builder-test")
            .Options;

        using TestDbContext context = new(options);
        IModel model = context.Model;

        IEntityType? entity = model.FindEntityType(typeof(SettingRecord));

        entity.ShouldNotBeNull();
        entity.GetTableName().ShouldBe("settings_setting_records");
    }

    [Fact]
    public void ConfigureSettingsModule_CreatedAt_IsRequired()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("model-builder-createdat-test")
            .Options;

        using TestDbContext context = new(options);
        IEntityType entity = context.Model.FindEntityType(typeof(SettingRecord))!;

        IProperty createdAt = entity.FindProperty(nameof(SettingRecord.CreatedAt))!;
        createdAt.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void ConfigureSettingsModule_CreatedBy_HasMaxLength256()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("model-builder-createdby-test")
            .Options;

        using TestDbContext context = new(options);
        IEntityType entity = context.Model.FindEntityType(typeof(SettingRecord))!;

        IProperty createdBy = entity.FindProperty(nameof(SettingRecord.CreatedBy))!;
        createdBy.GetMaxLength().ShouldBe(256);
    }

    [Fact]
    public void ConfigureSettingsModule_ModifiedBy_HasMaxLength256()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("model-builder-modifiedby-test")
            .Options;

        using TestDbContext context = new(options);
        IEntityType entity = context.Model.FindEntityType(typeof(SettingRecord))!;

        IProperty modifiedBy = entity.FindProperty(nameof(SettingRecord.ModifiedBy))!;
        modifiedBy.GetMaxLength().ShouldBe(256);
    }

    [Fact]
    public void ConfigureSettingsModule_ProviderKey_HasMaxLength256()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase("model-builder-providerkey-test")
            .Options;

        using TestDbContext context = new(options);
        IEntityType entity = context.Model.FindEntityType(typeof(SettingRecord))!;

        IProperty providerKey = entity.FindProperty(nameof(SettingRecord.ProviderKey))!;
        providerKey.GetMaxLength().ShouldBe(256);
    }
}
