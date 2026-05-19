using Granit.Features.EntityFrameworkCore.Entities;
using Granit.Features.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Features.EntityFrameworkCore.Tests;

public sealed class FeaturesModelBuilderExtensionsTests
{
    private static IModel BuildModel()
    {
        DbContextOptionsBuilder<FeaturesDbContext> optionsBuilder = new();
        optionsBuilder.UseInMemoryDatabase(Guid.NewGuid().ToString());

        using FeaturesDbContext context = new(optionsBuilder.Options, GranitDesignTime.CurrentTenant);
        return context.Model;
    }

    [Fact]
    public void ConfigureFeaturesModule_RegistersTenantFeatureOverrideEntity()
    {
        IModel model = BuildModel();

        IEntityType? entityType = model.FindEntityType(typeof(TenantFeatureOverride));

        entityType.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureFeaturesModule_SetsTableName()
    {
        IModel model = BuildModel();

        string? tableName = model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .GetTableName();

        tableName.ShouldBe("features_overrides");
    }

    [Fact]
    public void ConfigureFeaturesModule_ConfiguresUniqueIndex()
    {
        IModel model = BuildModel();

        IEntityType entityType = model.FindEntityType(typeof(TenantFeatureOverride))!;

        IIndex? uniqueIndex = entityType.GetIndexes().FirstOrDefault(i =>
            i.IsUnique &&
            i.Properties.Any(p => p.Name == nameof(TenantFeatureOverride.TenantId)) &&
            i.Properties.Any(p => p.Name == nameof(TenantFeatureOverride.FeatureName)));

        uniqueIndex.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureFeaturesModule_FeatureName_HasMaxLength200()
    {
        IModel model = BuildModel();

        IProperty? property = model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .FindProperty(nameof(TenantFeatureOverride.FeatureName));

        property.ShouldNotBeNull();
        property!.GetMaxLength().ShouldBe(200);
    }

    [Fact]
    public void ConfigureFeaturesModule_Value_HasMaxLength2000()
    {
        IModel model = BuildModel();

        IProperty? property = model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .FindProperty(nameof(TenantFeatureOverride.Value));

        property.ShouldNotBeNull();
        property!.GetMaxLength().ShouldBe(2000);
    }
}
