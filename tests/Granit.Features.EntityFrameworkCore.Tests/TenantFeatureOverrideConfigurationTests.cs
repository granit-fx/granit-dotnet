using Granit.Features.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Features.EntityFrameworkCore.Tests;

public sealed class TenantFeatureOverrideConfigurationTests
{
    private static GranitFeaturesDbContext CreateInMemory() =>
        new(new DbContextOptionsBuilder<GranitFeaturesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // -------------------------------------------------------------------------
    // Audit columns constraints
    // -------------------------------------------------------------------------

    [Fact]
    public void CreatedAt_IsRequired()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        IProperty property = ctx.Model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .FindProperty(nameof(TenantFeatureOverride.CreatedAt))!;

        property.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void CreatedBy_HasMaxLength450_AndIsRequired()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        IProperty property = ctx.Model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .FindProperty(nameof(TenantFeatureOverride.CreatedBy))!;

        property.GetMaxLength().ShouldBe(450);
        property.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void ModifiedBy_HasMaxLength450()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        IProperty property = ctx.Model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .FindProperty(nameof(TenantFeatureOverride.ModifiedBy))!;

        property.GetMaxLength().ShouldBe(450);
    }

    [Fact]
    public void ModifiedAt_IsNullable()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        IProperty property = ctx.Model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .FindProperty(nameof(TenantFeatureOverride.ModifiedAt))!;

        property.IsNullable.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Index naming
    // -------------------------------------------------------------------------

    [Fact]
    public void UniqueIndex_HasExpectedDatabaseName()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        IEntityType entityType = ctx.Model.FindEntityType(typeof(TenantFeatureOverride))!;

        IIndex? index = entityType.GetIndexes().FirstOrDefault(i =>
            i.IsUnique &&
            i.Properties.Any(p => p.Name == nameof(TenantFeatureOverride.TenantId)) &&
            i.Properties.Any(p => p.Name == nameof(TenantFeatureOverride.FeatureName)));

        index.ShouldNotBeNull();
        index.GetDatabaseName().ShouldBe("uq_feature_overrides_tenant_feature");
    }

    // -------------------------------------------------------------------------
    // TenantId column
    // -------------------------------------------------------------------------

    [Fact]
    public void TenantId_IsNullable()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        IProperty property = ctx.Model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .FindProperty(nameof(TenantFeatureOverride.TenantId))!;

        property.IsNullable.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Primary key
    // -------------------------------------------------------------------------

    [Fact]
    public void PrimaryKey_IsId()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        IEntityType entityType = ctx.Model.FindEntityType(typeof(TenantFeatureOverride))!;
        IKey? pk = entityType.FindPrimaryKey();

        pk.ShouldNotBeNull();
        pk.Properties.ShouldHaveSingleItem()
            .Name.ShouldBe(nameof(TenantFeatureOverride.Id));
    }
}
