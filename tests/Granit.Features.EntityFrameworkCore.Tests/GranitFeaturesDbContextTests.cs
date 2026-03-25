using Granit.Features.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Features.EntityFrameworkCore.Tests;

public sealed class GranitFeaturesDbContextTests
{
    private static GranitFeaturesDbContext CreateInMemory() =>
        new(new DbContextOptionsBuilder<GranitFeaturesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // -------------------------------------------------------------------------
    // Schema creation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EnsureCreatedAsync_WithInMemoryProvider_DoesNotThrow()
    {
        await using GranitFeaturesDbContext ctx = CreateInMemory();

        Func<Task> act = () => ctx.Database.EnsureCreatedAsync(
            TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    // -------------------------------------------------------------------------
    // Entity configuration — model metadata
    // -------------------------------------------------------------------------

    [Fact]
    public void Model_TableName_IsSaasFeatureOverrides()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        string? tableName = ctx.Model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .GetTableName();

        tableName.ShouldBe("features_overrides");
    }

    [Fact]
    public void Model_UniqueIndex_OnTenantIdAndFeatureName()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        IEntityType entityType = ctx.Model.FindEntityType(typeof(TenantFeatureOverride))!;

        IIndex? uniqueIndex = entityType.GetIndexes().FirstOrDefault(i =>
            i.IsUnique &&
            i.Properties.Any(p => p.Name == nameof(TenantFeatureOverride.TenantId)) &&
            i.Properties.Any(p => p.Name == nameof(TenantFeatureOverride.FeatureName)));

        uniqueIndex.ShouldNotBeNull("a unique composite index on (TenantId, FeatureName) must be configured");
    }

    [Fact]
    public void Model_FeatureName_HasMaxLength200()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .FindProperty(nameof(TenantFeatureOverride.FeatureName));

        property!.GetMaxLength().ShouldBe(200);
        property.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_Value_HasMaxLength2000()
    {
        using GranitFeaturesDbContext ctx = CreateInMemory();

        IProperty? property = ctx.Model
            .FindEntityType(typeof(TenantFeatureOverride))!
            .FindProperty(nameof(TenantFeatureOverride.Value));

        property!.GetMaxLength().ShouldBe(2000);
        property.IsNullable.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // CRUD round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveAndReload_AllFields_MatchOriginal()
    {
        await using GranitFeaturesDbContext ctx = CreateInMemory();
        var tenantId = Guid.NewGuid();

        TenantFeatureOverride entity = new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FeatureName = "Acme.MaxUsersCount",
            Value = "5000",
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "admin@digitaldynamics.be",
        };

        ctx.FeatureOverrides.Add(entity);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();

        TenantFeatureOverride? loaded = await ctx.FeatureOverrides
            .FindAsync([entity.Id], TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded!.TenantId.ShouldBe(tenantId);
        loaded.FeatureName.ShouldBe("Acme.MaxUsersCount");
        loaded.Value.ShouldBe("5000");
        loaded.CreatedBy.ShouldBe("admin@digitaldynamics.be");
        loaded.CreatedAt.ShouldBe(entity.CreatedAt);
    }
}
