using Granit.Testing.Fakes;
using Shouldly;

namespace Granit.Testing.EntityFrameworkCore.Tests;

public sealed class SqliteDbContextFactoryAdditionalTests
{
    [Fact]
    public void CreateContext_With_All_Custom_Fakes()
    {
        FakeCurrentTenant tenant = new();
        FakeCurrentUser user = new();
        FakeClock clock = new();
        FakeGuidGenerator guidGenerator = new();

        using SqliteDbContextFactory<TestDbContext> factory = new(
            tenant: tenant,
            user: user,
            clock: clock,
            guidGenerator: guidGenerator);

        using TestDbContext context = factory.CreateContext();

        context.ShouldNotBeNull();
    }

    [Fact]
    public void CreateContext_With_ConfigureOptions_Callback()
    {
        bool callbackInvoked = false;

        using SqliteDbContextFactory<TestDbContext> factory = new(
            configureOptions: _ => callbackInvoked = true);

        using TestDbContext context = factory.CreateContext();

        callbackInvoked.ShouldBeTrue();
    }

    [Fact]
    public void CreateContext_With_EnsureCreated_False()
    {
        using SqliteDbContextFactory<TestDbContext> factory = new();

        // First create with ensureCreated=true to set up schema
        using TestDbContext ctx1 = factory.CreateContext(ensureCreated: true);

        // Second create with ensureCreated=false should still work
        using TestDbContext ctx2 = factory.CreateContext(ensureCreated: false);
        ctx2.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateContext_Uses_Provided_Clock()
    {
        FakeClock clock = new();
        DateTimeOffset customTime = new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        clock.Now = customTime;

        using SqliteDbContextFactory<TestDbContext> factory = new(clock: clock);
        using TestDbContext context = factory.CreateContext();

        TestAuditedEntity entity = new() { Name = "ClockTest" };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.CreatedAt.ShouldBe(customTime);
    }

    [Fact]
    public async Task CreateContext_Uses_Provided_User()
    {
        FakeCurrentUser user = new();
        user.UserId = "custom-user-id";

        using SqliteDbContextFactory<TestDbContext> factory = new(user: user);
        using TestDbContext context = factory.CreateContext();

        TestAuditedEntity entity = new() { Name = "UserTest" };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.CreatedBy.ShouldBe("custom-user-id");
    }

    [Fact]
    public void Dispose_Is_Idempotent()
    {
        SqliteDbContextFactory<TestDbContext> factory = new();

        factory.Dispose();
        Should.NotThrow(() => factory.Dispose());
    }

    [Fact]
    public async Task CreateContext_Uses_Provided_GuidGenerator()
    {
        var expectedId = Guid.NewGuid();
        FakeGuidGenerator guidGenerator = new(expectedId);

        using SqliteDbContextFactory<TestDbContext> factory = new(guidGenerator: guidGenerator);
        using TestDbContext context = factory.CreateContext();

        TestAuditedEntity entity = new() { Name = "GuidTest" };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.Id.ShouldBe(expectedId);
    }
}
