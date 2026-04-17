using Granit.MultiTenancy.EntityFrameworkCore.Entities;
using Granit.MultiTenancy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.EntityFrameworkCore.Tests;

public sealed class TenantEntityTypeConfigurationTests
{
    private static MultiTenancyDbContext CreateInMemory() =>
        new(new DbContextOptionsBuilder<MultiTenancyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Model_TableName_IsTenantsTenants()
    {
        using MultiTenancyDbContext ctx = CreateInMemory();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(Tenant))!;

        string? tableName = entityType.GetTableName();

        tableName.ShouldBe("tenants_tenants");
    }

    [Fact]
    public void Model_UniqueIndex_OnIdentifier()
    {
        using MultiTenancyDbContext ctx = CreateInMemory();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(Tenant))!;

        IIndex? uniqueIndex = entityType.GetIndexes().FirstOrDefault(i =>
            i.IsUnique &&
            i.Properties.Any(p => p.Name == nameof(Tenant.Identifier)));

        uniqueIndex.ShouldNotBeNull("a unique index on Identifier must be configured");
    }

    [Fact]
    public void Model_Name_HasMaxLength256()
    {
        using MultiTenancyDbContext ctx = CreateInMemory();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(Tenant))!;

        IProperty name = entityType.FindProperty(nameof(Tenant.Name))!;

        name.GetMaxLength().ShouldBe(256);
        name.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_Identifier_HasMaxLength64()
    {
        using MultiTenancyDbContext ctx = CreateInMemory();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(Tenant))!;

        IProperty identifier = entityType.FindProperty(nameof(Tenant.Identifier))!;

        identifier.GetMaxLength().ShouldBe(64);
        identifier.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_ContactEmail_HasMaxLength256()
    {
        using MultiTenancyDbContext ctx = CreateInMemory();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(Tenant))!;

        IProperty email = entityType.FindProperty(nameof(Tenant.ContactEmail))!;

        email.GetMaxLength().ShouldBe(256);
        email.IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void Model_Activated_IsRequired()
    {
        using MultiTenancyDbContext ctx = CreateInMemory();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(Tenant))!;

        IProperty activated = entityType.FindProperty(nameof(Tenant.Activated))!;

        activated.IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void Model_Jurisdiction_HasMaxLength16()
    {
        using MultiTenancyDbContext ctx = CreateInMemory();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(Tenant))!;

        IProperty jurisdiction = entityType.FindProperty(nameof(Tenant.Jurisdiction))!;

        jurisdiction.GetMaxLength().ShouldBe(16);
        jurisdiction.IsNullable.ShouldBeTrue();
    }
}
