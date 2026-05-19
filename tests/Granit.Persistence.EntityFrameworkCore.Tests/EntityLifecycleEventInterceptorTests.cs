using Granit.Domain;
using Granit.Events;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class EntityLifecycleEventInterceptorTests
{
    private readonly IDomainEventDispatcher _domainDispatcher;
    private readonly IIntegrationEventDispatcher _integrationDispatcher;

    public EntityLifecycleEventInterceptorTests()
    {
        _domainDispatcher = Substitute.For<IDomainEventDispatcher>();
        _domainDispatcher.DispatchAsync(Arg.Any<IReadOnlyList<IDomainEvent>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        _integrationDispatcher = Substitute.For<IIntegrationEventDispatcher>();
        _integrationDispatcher.DispatchAsync(Arg.Any<IReadOnlyList<IIntegrationEvent>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    // -------------------------------------------------------------------------
    // IEmitEntityLifecycleEvents — local domain events
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_AddedEntity_EmitsEntityCreatedEvent()
    {
        await using TestDbContext context = CreateContext();
        context.LifecycleEntities.Add(new TestLifecycleEntity());

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _domainDispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyList<IDomainEvent>>(events =>
                events.Count == 1 && events[0] is EntityCreatedEvent<TestLifecycleEntity>),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedEntity_EmitsEntityUpdatedEvent()
    {
        await using TestDbContext context = CreateContext();
        TestLifecycleEntity entity = new();
        context.LifecycleEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _domainDispatcher.ClearReceivedCalls();

        entity.Name = "updated";
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _domainDispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyList<IDomainEvent>>(events =>
                events.Count == 1 && events[0] is EntityUpdatedEvent<TestLifecycleEntity>),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_HardDeletedEntity_EmitsEntityDeletedEvent()
    {
        await using TestDbContext context = CreateContext();
        TestLifecycleEntity entity = new();
        context.LifecycleEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _domainDispatcher.ClearReceivedCalls();

        context.LifecycleEntities.Remove(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _domainDispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyList<IDomainEvent>>(events =>
                events.Count == 1 && events[0] is EntityDeletedEvent<TestLifecycleEntity>),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_SoftDeletedEntity_EmitsEntityDeletedEvent()
    {
        await using TestDbContext context = CreateContext();
        TestSoftDeletableLifecycleEntity entity = new();
        context.SoftDeletableEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _domainDispatcher.ClearReceivedCalls();

        // Simulate soft delete: IsDeleted false → true
        entity.IsDeleted = true;
        entity.DeletedAt = DateTimeOffset.UtcNow;
        context.Entry(entity).State = EntityState.Modified;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _domainDispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyList<IDomainEvent>>(events =>
                events.Count == 1 && events[0] is EntityDeletedEvent<TestSoftDeletableLifecycleEntity>),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_PlainEntity_DoesNotEmitLifecycleEvents()
    {
        await using TestDbContext context = CreateContext();
        context.PlainEntities.Add(new TestPlainEntity { Name = "plain" });

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _domainDispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<IReadOnlyList<IDomainEvent>>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // IHasEntityEto — local + distributed events
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SaveChangesAsync_AddedEntityWithEto_EmitsBothEvents()
    {
        await using TestDbContext context = CreateContext();
        context.EtoEntities.Add(new TestEntityWithEto());

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _domainDispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyList<IDomainEvent>>(events =>
                events.Count == 1 && events[0] is EntityCreatedEvent<TestEntityWithEto>),
            Arg.Any<CancellationToken>());

        await _integrationDispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyList<IIntegrationEvent>>(events =>
                events.Count == 1 && events[0] is EntityCreatedEto<TestEto>),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_DeletedEntityWithEto_EmitsEntityDeletedEto()
    {
        await using TestDbContext context = CreateContext();
        TestEntityWithEto entity = new();
        context.EtoEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _integrationDispatcher.ClearReceivedCalls();

        context.EtoEntities.Remove(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _integrationDispatcher.Received(1).DispatchAsync(
            Arg.Is<IReadOnlyList<IIntegrationEvent>>(events =>
                events.Count == 1 && events[0] is EntityDeletedEto<TestEto>),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveChangesAsync_LocalOnlyEntity_DoesNotEmitIntegrationEvents()
    {
        await using TestDbContext context = CreateContext();
        context.LifecycleEntities.Add(new TestLifecycleEntity());

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _integrationDispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<IReadOnlyList<IIntegrationEvent>>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private TestDbContext CreateContext()
    {
        EntityLifecycleEventInterceptor interceptor = new(_domainDispatcher, _integrationDispatcher);
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        return new TestDbContext(options);
    }

    // -------------------------------------------------------------------------
    // Test fixtures
    // -------------------------------------------------------------------------

    private sealed record TestEto(string Name);

    private sealed class TestLifecycleEntity : Entity, IEmitEntityLifecycleEvents
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestSoftDeletableLifecycleEntity : Entity, IEmitEntityLifecycleEvents, ISoftDeletable
    {
        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }

    private sealed class TestEntityWithEto : Entity, IHasEntityEto<TestEto>
    {
        public string Name { get; set; } = string.Empty;
        public TestEto ToEto() => new(Name);
    }

    private sealed class TestPlainEntity : Entity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDbContext(DbContextOptions<EntityLifecycleEventInterceptorTests.TestDbContext> options)
        : DbContext(options)
    {
        public DbSet<TestLifecycleEntity> LifecycleEntities => Set<TestLifecycleEntity>();
        public DbSet<TestSoftDeletableLifecycleEntity> SoftDeletableEntities => Set<TestSoftDeletableLifecycleEntity>();
        public DbSet<TestEntityWithEto> EtoEntities => Set<TestEntityWithEto>();
        public DbSet<TestPlainEntity> PlainEntities => Set<TestPlainEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestLifecycleEntity>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<TestSoftDeletableLifecycleEntity>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<TestEntityWithEto>().Property(e => e.Id).ValueGeneratedNever();
            modelBuilder.Entity<TestPlainEntity>().Property(e => e.Id).ValueGeneratedNever();
        }
    }
}
