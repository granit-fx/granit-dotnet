using Granit.Domain.ValueObjects;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class ImageDimensionsModelBuilderExtensionsTests
{
    [Fact]
    public void MapImageDimensions_MapsToFlatColumns_AndIsNotAnEntityType()
    {
        using MediaDbContext context = new(new DbContextOptionsBuilder<MediaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        IReadOnlyList<string> entityNames = [.. context.Model.GetEntityTypes().Select(et => et.ClrType.Name)];
        entityNames.ShouldNotContain(nameof(ImageDimensions), "ImageDimensions must be a complex type, not an entity");

        IEntityType owner = context.Model.FindEntityType(typeof(MediaEntity))!;
        IComplexProperty complex = owner.FindComplexProperty(nameof(MediaEntity.Dimensions))
            .ShouldNotBeNull("Dimensions must be mapped as a complex property");

        complex.IsNullable.ShouldBeTrue("an ImageDimensions? property must map to a nullable complex type");
        complex.ComplexType.GetProperties().Select(p => p.GetColumnName())
            .ShouldBe(["Width", "Height"], ignoreOrder: true);
    }

    [Fact]
    public void MapImageDimensions_HonoursCustomColumnNames()
    {
        using CustomColumnDbContext context = new(new DbContextOptionsBuilder<CustomColumnDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

        IComplexProperty complex = context.Model
            .FindEntityType(typeof(MediaEntity))!
            .FindComplexProperty(nameof(MediaEntity.Dimensions))!;

        complex.ComplexType.GetProperties().Select(p => p.GetColumnName())
            .ShouldBe(["px_w", "px_h"], ignoreOrder: true);
    }

    public sealed class MediaEntity
    {
        public Guid Id { get; set; }
        public ImageDimensions? Dimensions { get; set; }
    }

    private sealed class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options)
    {
        public DbSet<MediaEntity> Media => Set<MediaEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MediaEntity>().MapImageDimensions(e => e.Dimensions);
            modelBuilder.ApplyGranitConventions();
        }
    }

    private sealed class CustomColumnDbContext(DbContextOptions<CustomColumnDbContext> options) : DbContext(options)
    {
        public DbSet<MediaEntity> Media => Set<MediaEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MediaEntity>().MapImageDimensions(e => e.Dimensions, "px_w", "px_h");
            modelBuilder.ApplyGranitConventions();
        }
    }
}
