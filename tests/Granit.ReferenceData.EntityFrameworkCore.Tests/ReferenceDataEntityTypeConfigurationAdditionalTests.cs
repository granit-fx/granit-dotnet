using Granit.ReferenceData.EntityFrameworkCore.Extensions;
using Granit.ReferenceData.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.EntityFrameworkCore.Tests;

public sealed class ReferenceDataEntityTypeConfigurationAdditionalTests
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

    // -------------------------------------------------------------------------
    // Translation label max lengths
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(nameof(TestEntity.LabelFr))]
    [InlineData(nameof(TestEntity.LabelNl))]
    [InlineData(nameof(TestEntity.LabelDe))]
    [InlineData(nameof(TestEntity.LabelEs))]
    [InlineData(nameof(TestEntity.LabelIt))]
    [InlineData(nameof(TestEntity.LabelPt))]
    [InlineData(nameof(TestEntity.LabelZh))]
    [InlineData(nameof(TestEntity.LabelJa))]
    [InlineData(nameof(TestEntity.LabelPl))]
    [InlineData(nameof(TestEntity.LabelTr))]
    [InlineData(nameof(TestEntity.LabelKo))]
    [InlineData(nameof(TestEntity.LabelSv))]
    [InlineData(nameof(TestEntity.LabelCs))]
    public void TranslationLabel_HasMaxLength_250(string propertyName)
    {
        IEntityType entityType = GetEntityType();
        IProperty property = entityType.FindProperty(propertyName)!;

        property.GetMaxLength().ShouldBe(250);
    }

    // -------------------------------------------------------------------------
    // SortOrder has default value
    // -------------------------------------------------------------------------

    [Fact]
    public void SortOrder_HasDefaultValue_Zero()
    {
        IEntityType entityType = GetEntityType();
        IProperty sortOrder = entityType.FindProperty(nameof(TestEntity.SortOrder))!;

        sortOrder.GetDefaultValue().ShouldBe(0);
    }

    // -------------------------------------------------------------------------
    // ModifiedBy has max length
    // -------------------------------------------------------------------------

    [Fact]
    public void ModifiedBy_HasMaxLength_256()
    {
        IEntityType entityType = GetEntityType();
        IProperty modifiedBy = entityType.FindProperty(nameof(TestEntity.ModifiedBy))!;

        modifiedBy.GetMaxLength().ShouldBe(256);
    }

    // -------------------------------------------------------------------------
    // HasKey on Id
    // -------------------------------------------------------------------------

    [Fact]
    public void PrimaryKey_Is_Id()
    {
        IEntityType entityType = GetEntityType();
        IKey? key = entityType.FindPrimaryKey();

        key.ShouldNotBeNull();
        key!.Properties.Count.ShouldBe(1);
        key.Properties[0].Name.ShouldBe(nameof(TestEntity.Id));
    }

    // -------------------------------------------------------------------------
    // Code unique index has expected name
    // -------------------------------------------------------------------------

    [Fact]
    public void Code_UniqueIndex_HasExpectedDatabaseName()
    {
        IEntityType entityType = GetEntityType();
        IProperty code = entityType.FindProperty(nameof(TestEntity.Code))!;

        IIndex? index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0] == code);

        index.ShouldNotBeNull();
        index!.GetDatabaseName().ShouldBe("uq_ref_test_entities_code");
    }

    // -------------------------------------------------------------------------
    // IsActive index has expected name
    // -------------------------------------------------------------------------

    [Fact]
    public void IsActive_Index_HasExpectedDatabaseName()
    {
        IEntityType entityType = GetEntityType();
        IProperty isActive = entityType.FindProperty(nameof(TestEntity.IsActive))!;

        IIndex? index = entityType.GetIndexes()
            .FirstOrDefault(i => i.Properties.Count == 1 && i.Properties[0] == isActive);

        index.ShouldNotBeNull();
        index!.GetDatabaseName().ShouldBe("ix_ref_test_entities_is_active");
    }
}
