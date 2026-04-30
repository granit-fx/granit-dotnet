// =============================================================================
// Tests - EntityViewDbContext (schema + entity configuration)
// =============================================================================

using Granit.Entities.Views.Domain;
using Granit.Entities.Views.EntityFrameworkCore;
using Granit.Entities.Views.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Entities.Views.EntityFrameworkCore.Tests;

public sealed class EntityViewDbContextTests
{
    private static EntityViewDbContext CreateInMemory() =>
        new(new DbContextOptionsBuilder<EntityViewDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task EnsureCreatedAsync_WithInMemoryProvider_DoesNotThrow()
    {
        await using EntityViewDbContext ctx = CreateInMemory();
        Func<Task> act = () => ctx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        await Should.NotThrowAsync(act);
    }

    [Fact]
    public void Model_TableName_FollowsConvention()
    {
        using EntityViewDbContext ctx = CreateInMemory();

        string? tableName = ctx.Model.FindEntityType(typeof(EntityView))!.GetTableName();
        tableName.ShouldBe(GranitEntitiesViewsDbProperties.DbTablePrefix + "entity_views");
    }

    [Fact]
    public void Model_HasCompositeIndex_OnTenantIdAndEntityName()
    {
        using EntityViewDbContext ctx = CreateInMemory();

        IEntityType entityType = ctx.Model.FindEntityType(typeof(EntityView))!;
        IIndex? composite = entityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Count == 2
            && i.Properties.Any(p => p.Name == nameof(EntityView.TenantId))
            && i.Properties.Any(p => p.Name == nameof(EntityView.EntityName)));

        composite.ShouldNotBeNull();
    }

    [Fact]
    public void Model_HasIndex_OnOwnerId()
    {
        using EntityViewDbContext ctx = CreateInMemory();

        IEntityType entityType = ctx.Model.FindEntityType(typeof(EntityView))!;
        IIndex? ownerIndex = entityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Count == 1 && i.Properties[0].Name == nameof(EntityView.OwnerId));

        ownerIndex.ShouldNotBeNull();
    }

    [Fact]
    public void Model_StateProperty_HasJsonConverter()
    {
        using EntityViewDbContext ctx = CreateInMemory();

        IProperty stateProp = ctx.Model.FindEntityType(typeof(EntityView))!
            .FindProperty(nameof(EntityView.State))!;

        stateProp.GetValueConverter().ShouldNotBeNull();
        stateProp.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_SharedWithProperty_HasJsonConverter_AndIsNullable()
    {
        using EntityViewDbContext ctx = CreateInMemory();

        IProperty sharedProp = ctx.Model.FindEntityType(typeof(EntityView))!
            .FindProperty(nameof(EntityView.SharedWith))!;

        sharedProp.GetValueConverter().ShouldNotBeNull();
        sharedProp.IsNullable.ShouldBeTrue();
    }
}
