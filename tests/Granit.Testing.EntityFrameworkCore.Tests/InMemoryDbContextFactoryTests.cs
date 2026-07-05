using Granit.Domain;
using Granit.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Granit.Testing.EntityFrameworkCore.Tests;

public sealed class InMemoryDbContextFactoryTests
{
    [Fact]
    public void CreateContext_Returns_Usable_Context()
    {
        InMemoryDbContextFactory<TestDbContext> factory = new();

        using TestDbContext context = factory.CreateContext();

        context.ShouldNotBeNull();
        context.Database.IsInMemory().ShouldBeTrue();
    }

    [Fact]
    public async Task AuditInterceptor_Fires_On_Add()
    {
        FakeClock clock = new();
        FakeCurrentUser user = new();
        InMemoryDbContextFactory<TestDbContext> factory = new(user: user, clock: clock);

        await using TestDbContext context = factory.CreateContext();
        TestAuditedEntity entity = new() { Name = "Test" };
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
        InMemoryDbContextFactory<TestDbContext> factory = new(user: user, clock: clock);

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
        InMemoryDbContextFactory<TestDbContext> factory = new();

        await using TestDbContext ctx1 = factory.CreateContext();
        ctx1.AuditedEntities.Add(new TestAuditedEntity { Name = "Shared" });
        await ctx1.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using TestDbContext ctx2 = factory.CreateContext();
        (await ctx2.AuditedEntities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }
}

// ---- Shared test entities and DbContext ----

public sealed class TestAuditedEntity : AuditedEntity
{
    public string Name { get; set; } = string.Empty;
}

public sealed class TestFullAuditedEntity : FullAuditedEntity
{
    public string Name { get; set; } = string.Empty;
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestAuditedEntity> AuditedEntities => Set<TestAuditedEntity>();
    public DbSet<TestFullAuditedEntity> FullAuditedEntities => Set<TestFullAuditedEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestAuditedEntity>().Property(e => e.Id).ValueGeneratedNever();
        modelBuilder.Entity<TestFullAuditedEntity>().Property(e => e.Id).ValueGeneratedNever();
    }
}
