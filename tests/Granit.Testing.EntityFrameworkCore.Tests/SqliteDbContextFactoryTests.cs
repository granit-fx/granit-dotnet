using Granit.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Granit.Testing.EntityFrameworkCore.Tests;

public sealed class SqliteDbContextFactoryTests : IDisposable
{
    private readonly SqliteDbContextFactory<TestDbContext> _factory;

    public SqliteDbContextFactoryTests()
    {
        _factory = new SqliteDbContextFactory<TestDbContext>();
    }

    [Fact]
    public void CreateContext_Returns_Usable_Context()
    {
        using TestDbContext context = _factory.CreateContext();

        context.ShouldNotBeNull();
    }

    [Fact]
    public async Task Schema_Is_Created_By_Default()
    {
        await using TestDbContext context = _factory.CreateContext();

        (await context.AuditedEntities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task AuditInterceptor_Fires_On_Add()
    {
        FakeClock clock = new();
        FakeCurrentUser user = new();
        using SqliteDbContextFactory<TestDbContext> factory = new(user: user, clock: clock);

        await using TestDbContext context = factory.CreateContext();
        TestAuditedEntity entity = new() { Name = "SqliteTest" };
        context.AuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.CreatedAt.ShouldBe(clock.Now);
        entity.CreatedBy.ShouldBe(user.UserId);
        entity.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task SoftDeleteInterceptor_Fires_On_Delete()
    {
        FakeClock clock = new();
        FakeCurrentUser user = new();
        using SqliteDbContextFactory<TestDbContext> factory = new(user: user, clock: clock);

        await using TestDbContext context = factory.CreateContext();
        TestFullAuditedEntity entity = new() { Name = "ToDelete" };
        context.FullAuditedEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.FullAuditedEntities.Remove(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.IsDeleted.ShouldBeTrue();
        entity.DeletedAt.ShouldBe(clock.Now);
        entity.DeletedBy.ShouldBe(user.UserId);
    }

    [Fact]
    public async Task Contexts_Share_Same_Database()
    {
        await using TestDbContext ctx1 = _factory.CreateContext();
        ctx1.AuditedEntities.Add(new TestAuditedEntity { Name = "Shared" });
        await ctx1.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using TestDbContext ctx2 = _factory.CreateContext(ensureCreated: false);
        (await ctx2.AuditedEntities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public void Dispose_Prevents_Further_CreateContext()
    {
        SqliteDbContextFactory<TestDbContext> factory = new();
        factory.Dispose();

        Should.Throw<ObjectDisposedException>(() => factory.CreateContext());
    }

    public void Dispose() => _factory.Dispose();
}
