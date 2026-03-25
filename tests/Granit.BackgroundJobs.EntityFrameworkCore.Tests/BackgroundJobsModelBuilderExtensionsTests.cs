using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Tests;

/// <summary>
/// Tests for <see cref="BackgroundJobsModelBuilderExtensions.ConfigureBackgroundJobsModule"/>
/// and <see cref="GranitBackgroundJobsDbProperties"/> — verifies that the host-owned
/// migration extension produces the expected EF Core model.
/// </summary>
public sealed class BackgroundJobsModelBuilderExtensionsTests
{
    /// <summary>
    /// Simulates a host-owned DbContext that calls <c>ConfigureBackgroundJobsModule()</c>.
    /// </summary>
    private sealed class HostDbContext(DbContextOptions<HostDbContext> options)
        : DbContext(options)
    {
        public DbSet<BackgroundJobDefinition> Jobs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ConfigureBackgroundJobsModule();
        }
    }

    private static IModel BuildModel()
    {
        DbContextOptions<HostDbContext> options =
            new DbContextOptionsBuilder<HostDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        using HostDbContext context = new(options);
        return context.Model;
    }

    // =========================================================================
    // Default DbProperties
    // =========================================================================

    [Fact]
    public void ConfigureBackgroundJobsModule_MapsToDefaultTable()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(BackgroundJobDefinition))!;

        entity.GetTableName().ShouldBe("background_jobs_background_jobs");
    }

    [Fact]
    public void ConfigureBackgroundJobsModule_HasPrimaryKeyOnId()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(BackgroundJobDefinition))!;

        IKey pk = entity.FindPrimaryKey()!;
        pk.Properties.ShouldContain(p => p.Name == nameof(BackgroundJobDefinition.Id));
    }

    [Fact]
    public void ConfigureBackgroundJobsModule_HasUniqueIndexOnJobName()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(BackgroundJobDefinition))!;

        IIndex? uniqueIndex = entity.GetIndexes()
            .FirstOrDefault(i =>
                i.IsUnique &&
                i.Properties.Any(p => p.Name == nameof(BackgroundJobDefinition.JobName)));

        uniqueIndex.ShouldNotBeNull("a unique index on JobName must be configured");
    }

    [Fact]
    public void ConfigureBackgroundJobsModule_JobName_HasMaxLength200()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(BackgroundJobDefinition))!;

        IProperty property = entity.FindProperty(nameof(BackgroundJobDefinition.JobName))!;

        property.GetMaxLength().ShouldBe(200);
        property.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void ConfigureBackgroundJobsModule_DefaultSchema_IsNull()
    {
        IModel model = BuildModel();
        IEntityType entity = model.FindEntityType(typeof(BackgroundJobDefinition))!;

        entity.GetSchema().ShouldBeNull();
    }
}
