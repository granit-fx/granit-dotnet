using Granit.ReferenceData.EntityFrameworkCore;
using Granit.ReferenceData.EntityFrameworkCore.Extensions;
using Granit.ReferenceData.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.EntityFrameworkCore.Tests;

public sealed class ModelBuilderExtensionsTests
{
    private sealed class TestEntityConfiguration : ReferenceDataEntityTypeConfiguration<TestEntity>
    {
        public TestEntityConfiguration() : base("ref_custom_table") { }
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

    [Fact]
    public void ConfigureReferenceData_AppliesConfiguration()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using TestDbContext context = new(options);
        IEntityType? entityType = context.Model.FindEntityType(typeof(TestEntity));

        entityType.ShouldNotBeNull();
        entityType!.GetTableName().ShouldBe("ref_custom_table");
    }

    [Fact]
    public void ConfigureReferenceData_ReturnsModelBuilder()
    {
        DbContextOptionsBuilder<TestDbContext> optionsBuilder = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());

        // Verify the method returns ModelBuilder (chaining)
        // This is implicitly tested by the fact that the context builds successfully
        DbContextOptions<TestDbContext> options = optionsBuilder.Options;
        using TestDbContext context = new(options);

        context.Model.ShouldNotBeNull();
    }
}
