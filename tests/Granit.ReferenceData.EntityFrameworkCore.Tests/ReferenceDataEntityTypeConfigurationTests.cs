using Granit.ReferenceData.EntityFrameworkCore;
using Granit.ReferenceData.EntityFrameworkCore.Extensions;
using Granit.ReferenceData.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.EntityFrameworkCore.Tests;

public sealed class ReferenceDataEntityTypeConfigurationTests
{
    private sealed class TestEntityConfiguration : ReferenceDataEntityTypeConfiguration<TestEntity>
    {
        public TestEntityConfiguration() : base("ref_test_entities") { }
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureReferenceData(new TestEntityConfiguration());
        }
    }

    private static IEntityType GetEntityType()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using TestDbContext context = new(options);
        return context.Model.FindEntityType(typeof(TestEntity))!;
    }

    [Fact]
    public void Table_Name_Is_Configured()
    {
        IEntityType entityType = GetEntityType();

        entityType.GetTableName().ShouldBe("ref_test_entities");
    }

    [Fact]
    public void Code_Has_MaxLength_50()
    {
        IEntityType entityType = GetEntityType();
        IProperty code = entityType.FindProperty(nameof(TestEntity.Code))!;

        code.GetMaxLength().ShouldBe(50);
    }

    [Fact]
    public void Code_Is_Required()
    {
        IEntityType entityType = GetEntityType();
        IProperty code = entityType.FindProperty(nameof(TestEntity.Code))!;

        code.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void LabelEn_Has_MaxLength_250()
    {
        IEntityType entityType = GetEntityType();
        IProperty labelEn = entityType.FindProperty(nameof(TestEntity.LabelEn))!;

        labelEn.GetMaxLength().ShouldBe(250);
    }

    [Fact]
    public void LabelEn_Is_Required()
    {
        IEntityType entityType = GetEntityType();
        IProperty labelEn = entityType.FindProperty(nameof(TestEntity.LabelEn))!;

        labelEn.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Label_Is_Not_Mapped()
    {
        IEntityType entityType = GetEntityType();
        IProperty? label = entityType.FindProperty(nameof(TestEntity.Label));

        label.ShouldBeNull();
    }

    [Fact]
    public void Code_Has_Unique_Index()
    {
        IEntityType entityType = GetEntityType();
        IProperty code = entityType.FindProperty(nameof(TestEntity.Code))!;

        IIndex? index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0] == code);

        index.ShouldNotBeNull();
        index!.IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void IsActive_Has_Index()
    {
        IEntityType entityType = GetEntityType();
        IProperty isActive = entityType.FindProperty(nameof(TestEntity.Activated))!;

        IIndex? index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0] == isActive);

        index.ShouldNotBeNull();
    }

    [Fact]
    public void CreatedAt_Is_Required()
    {
        IEntityType entityType = GetEntityType();
        IProperty createdAt = entityType.FindProperty(nameof(TestEntity.CreatedAt))!;

        createdAt.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void CreatedBy_Has_MaxLength_256()
    {
        IEntityType entityType = GetEntityType();
        IProperty createdBy = entityType.FindProperty(nameof(TestEntity.CreatedBy))!;

        createdBy.GetMaxLength().ShouldBe(256);
    }
}
