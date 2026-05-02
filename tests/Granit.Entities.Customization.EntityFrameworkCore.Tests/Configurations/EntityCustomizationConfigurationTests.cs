using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.EntityFrameworkCore;
using Granit.Entities.Customization.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.Entities.Customization.EntityFrameworkCore.Tests.Configurations;

public sealed class EntityCustomizationConfigurationTests
{
    private static IEntityType GetEntityType()
    {
        DbContextOptions<CustomizationDbContext> options = new DbContextOptionsBuilder<CustomizationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        using var ctx = new CustomizationDbContext(options);
        return ctx.Model.FindEntityType(typeof(EntityCustomization))
            ?? throw new InvalidOperationException("EntityCustomization entity type missing from model");
    }

    [Fact]
    public void Table_uses_configured_prefix()
    {
        IEntityType type = GetEntityType();
        type.GetTableName().ShouldBe(
            GranitEntitiesCustomizationDbProperties.DbTablePrefix + "entity_customizations");
    }

    [Fact]
    public void EntityName_property_has_max_length_256_and_required()
    {
        IProperty prop = GetEntityType().FindProperty(nameof(EntityCustomization.EntityName))!;
        prop.GetMaxLength().ShouldBe(256);
        prop.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void LayoutKind_is_persisted_as_string()
    {
        IProperty prop = GetEntityType().FindProperty(nameof(EntityCustomization.LayoutKind))!;
        // Provider type after conversion = string.
        prop.GetProviderClrType().ShouldBe(typeof(string));
        prop.GetMaxLength().ShouldBe(32);
        prop.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Deltas_is_persisted_as_string_via_value_converter()
    {
        IProperty prop = GetEntityType().FindProperty(nameof(EntityCustomization.Deltas))!;
        prop.IsNullable.ShouldBeFalse();
        prop.GetValueConverter().ShouldNotBeNull();
        prop.GetValueComparer().ShouldNotBeNull();
        // Provider type assertion (string) requires a relational provider —
        // the InMemory provider used here returns null. The DeltaJsonRoundTrip
        // tests cover the actual string-column persistence on SQLite.
    }

    [Fact]
    public void Lookup_index_is_unique_and_covers_tenant_entity_layout()
    {
        IIndex index = GetEntityType().GetIndexes()
            .ShouldHaveSingleItem();

        index.IsUnique.ShouldBeTrue();
        index.Properties.Select(p => p.Name).ShouldBe(
            ["TenantId", "EntityName", "LayoutKind"]);
        index.GetDatabaseName().ShouldBe(
            $"ix_{GranitEntitiesCustomizationDbProperties.DbTablePrefix}entity_customizations_lookup");
    }

    [Fact]
    public void Audit_columns_have_max_length_256()
    {
        IEntityType type = GetEntityType();
        type.FindProperty(nameof(EntityCustomization.CreatedBy))!.GetMaxLength().ShouldBe(256);
        type.FindProperty(nameof(EntityCustomization.ModifiedBy))!.GetMaxLength().ShouldBe(256);
        type.FindProperty(nameof(EntityCustomization.DeletedBy))!.GetMaxLength().ShouldBe(256);
    }
}
