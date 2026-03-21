using Granit.Testing.EntityFrameworkCore;
using Granit.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Granit.Testing.EntityFrameworkCore.Tests;

public sealed class InMemoryDbContextFactoryAdditionalTests
{
    [Fact]
    public void CreateContext_With_All_Custom_Fakes()
    {
        FakeCurrentTenant tenant = new();
        FakeCurrentUser user = new();
        FakeClock clock = new();
        FakeGuidGenerator guidGenerator = new();

        InMemoryDbContextFactory<TestDbContext> factory = new(
            tenant: tenant,
            user: user,
            clock: clock,
            guidGenerator: guidGenerator);

        using TestDbContext context = factory.CreateContext();

        context.ShouldNotBeNull();
        context.Database.IsInMemory().ShouldBeTrue();
    }

    [Fact]
    public void CreateContext_With_ConfigureOptions_Callback()
    {
        bool callbackInvoked = false;

        InMemoryDbContextFactory<TestDbContext> factory = new(
            configureOptions: _ => callbackInvoked = true);

        using TestDbContext context = factory.CreateContext();

        callbackInvoked.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateContext_Uses_Provided_GuidGenerator()
    {
        var expectedId = Guid.NewGuid();
        FakeGuidGenerator guidGenerator = new(expectedId);

        InMemoryDbContextFactory<TestDbContext> factory = new(guidGenerator: guidGenerator);

        using TestDbContext context = factory.CreateContext();
        TestAuditedEntity entity = new() { Name = "GuidTest" };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.Id.ShouldBe(expectedId);
    }

    [Fact]
    public async Task CreateContext_Uses_Provided_Tenant()
    {
        FakeCurrentTenant tenant = new();
        var tenantId = Guid.NewGuid();
        tenant.Id = tenantId;

        InMemoryDbContextFactory<TestDbContext> factory = new(tenant: tenant);

        using TestDbContext context = factory.CreateContext();
        context.ShouldNotBeNull();

        // Verify we can still add entities (proves the context is functional)
        TestAuditedEntity entity = new() { Name = "TenantTest" };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void CreateContext_With_Default_Fakes_Succeeds()
    {
        InMemoryDbContextFactory<TestDbContext> factory = new();

        using TestDbContext context = factory.CreateContext();

        context.ShouldNotBeNull();
    }

    [Fact]
    public async Task Multiple_Factories_Have_Separate_Databases()
    {
        InMemoryDbContextFactory<TestDbContext> factory1 = new();
        InMemoryDbContextFactory<TestDbContext> factory2 = new();

        using TestDbContext ctx1 = factory1.CreateContext();
        ctx1.AuditedEntities.Add(new TestAuditedEntity { Name = "Factory1" });
        await ctx1.SaveChangesAsync(TestContext.Current.CancellationToken);

        using TestDbContext ctx2 = factory2.CreateContext();
        int count = await ctx2.AuditedEntities.CountAsync(TestContext.Current.CancellationToken);

        count.ShouldBe(0); // different database
    }
}
