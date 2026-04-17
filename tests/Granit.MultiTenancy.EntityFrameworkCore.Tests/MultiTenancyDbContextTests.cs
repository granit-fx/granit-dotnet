using Granit.MultiTenancy.EntityFrameworkCore.Entities;
using Granit.MultiTenancy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.EntityFrameworkCore.Tests;

public sealed class MultiTenancyDbContextTests
{
    private static MultiTenancyDbContext CreateInMemory() =>
        new(new DbContextOptionsBuilder<MultiTenancyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public void Model_HasTenantEntityType()
    {
        using MultiTenancyDbContext ctx = CreateInMemory();

        IEntityType? entityType = ctx.Model.FindEntityType(typeof(Tenant));

        entityType.ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveAndReload_AllFields_MatchOriginal()
    {
        await using MultiTenancyDbContext ctx = CreateInMemory();

        var tenant = Tenant.Create(
            Guid.NewGuid(),
            "Acme Corp",
            "acme-corp",
            "admin@acme.com");

        ctx.Tenants.Add(tenant);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        ctx.ChangeTracker.Clear();

        Tenant? loaded = await ctx.Tenants
            .FindAsync([tenant.Id], TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Name.ShouldBe("Acme Corp");
        loaded.Identifier.ShouldBe("acme-corp");
        loaded.ContactEmail.ShouldBe("admin@acme.com");
        loaded.Activated.ShouldBeTrue();
    }
}
