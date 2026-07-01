using Granit.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Users;
using Granit.Workflow.Domain;
using Granit.Workflow.EntityFrameworkCore.Interceptors;
using Granit.Workflow.Events;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.EntityFrameworkCore.Tests;

public sealed class WorkflowTransitionInterceptorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid FixedGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
    private static readonly Guid FixedTenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    public WorkflowTransitionInterceptorTests()
    {
        _clock.Now.Returns(FixedNow);
        _guidGenerator.Create().Returns(FixedGuid);
        _currentUserService.UserId.Returns("user-42");
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(FixedTenantId);
    }

    // ========================================================================
    // Transition record creation
    // ========================================================================

    [Fact]
    public async Task SaveChanges_WhenStatusChanges_ShouldCreateTransitionRecord()
    {
        // Arrange
        using TestDbContext context = CreateContext();
        TestWorkflowEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Status = WorkflowLifecycleStatus.Draft,
        };
        context.Entities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — change status
        entity.Status = WorkflowLifecycleStatus.Published;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        List<WorkflowTransitionRecord> records = await context.WorkflowTransitionRecords
            .ToListAsync(TestContext.Current.CancellationToken);

        records.Count.ShouldBe(1);
        WorkflowTransitionRecord record = records[0];
        record.EntityType.ShouldBe("TestWorkflowEntity");
        record.EntityId.ShouldBe(entity.Id.ToString());
        record.PreviousState.ShouldBe("Draft");
        record.NewState.ShouldBe("Published");
        record.TransitionedAt.ShouldBe(FixedNow);
        record.TransitionedBy.ShouldBe("user-42");
        record.TenantId.ShouldBe(FixedTenantId);

        entity.DomainEvents.ShouldHaveSingleItem();
        WorkflowStateChangedEvent stateChangedEvent = entity.DomainEvents.OfType<WorkflowStateChangedEvent>().Single();
        stateChangedEvent.EntityType.ShouldBe("TestWorkflowEntity");
        stateChangedEvent.EntityId.ShouldBe(entity.Id.ToString());
        stateChangedEvent.PreviousState.ShouldBe("Draft");
        stateChangedEvent.NewState.ShouldBe("Published");
        stateChangedEvent.TransitionedBy.ShouldBe("user-42");
    }

    [Fact]
    public async Task SaveChanges_WhenStatusUnchanged_ShouldNotCreateRecord()
    {
        // Arrange
        using TestDbContext context = CreateContext();
        TestWorkflowEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Status = WorkflowLifecycleStatus.Draft,
        };
        context.Entities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — change name but not status
        entity.Name = "Updated";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        List<WorkflowTransitionRecord> records = await context.WorkflowTransitionRecords
            .ToListAsync(TestContext.Current.CancellationToken);
        records.ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveChanges_WhenNoTenant_ShouldStoreNullTenantId()
    {
        // Arrange
        _currentTenant.IsAvailable.Returns(false);

        using TestDbContext context = CreateContext();
        TestWorkflowEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Status = WorkflowLifecycleStatus.Draft,
        };
        context.Entities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        entity.Status = WorkflowLifecycleStatus.Published;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        WorkflowTransitionRecord record = await context.WorkflowTransitionRecords
            .SingleAsync(TestContext.Current.CancellationToken);
        record.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task SaveChanges_WhenNoUser_ShouldUseSystemUserId()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);

        using TestDbContext context = CreateContext();
        TestWorkflowEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Status = WorkflowLifecycleStatus.Draft,
        };
        context.Entities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        entity.Status = WorkflowLifecycleStatus.Published;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        WorkflowTransitionRecord record = await context.WorkflowTransitionRecords
            .SingleAsync(TestContext.Current.CancellationToken);
        record.TransitionedBy.ShouldBe("system");
    }

    // ========================================================================
    // Comment from WorkflowTransitionContext
    // ========================================================================

    [Fact]
    public async Task SaveChanges_WithTransitionComment_ShouldStoreComment()
    {
        // Arrange
        using IDisposable scope = WorkflowTransitionContext.SetComment("Validated by Dr. Martin");

        using TestDbContext context = CreateContext();
        TestWorkflowEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Status = WorkflowLifecycleStatus.Draft,
        };
        context.Entities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        entity.Status = WorkflowLifecycleStatus.Published;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        WorkflowTransitionRecord record = await context.WorkflowTransitionRecords
            .SingleAsync(TestContext.Current.CancellationToken);
        record.Comment.ShouldBe("Validated by Dr. Martin");
    }

    // ========================================================================
    // IPublishable synchronization
    // ========================================================================

    [Fact]
    public async Task SaveChanges_WhenPublished_ShouldSyncIsPublishedToTrue()
    {
        // Arrange
        using TestDbContext context = CreateContext();
        TestVersionedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Version = 1,
            LifecycleStatus = WorkflowLifecycleStatus.Published,
            IsPublished = false, // Intentionally wrong — interceptor should fix
        };
        context.VersionedEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.IsPublished.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveChanges_WhenDraft_ShouldSyncIsPublishedToFalse()
    {
        // Arrange
        using TestDbContext context = CreateContext();
        TestVersionedEntity entity = new()
        {
            Id = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Version = 1,
            LifecycleStatus = WorkflowLifecycleStatus.Draft,
            IsPublished = true, // Intentionally wrong
        };
        context.VersionedEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.IsPublished.ShouldBeFalse();
    }

    // ========================================================================
    // Explicit interface implementation (VersionedWorkflowEntity pattern)
    // ========================================================================

    [Fact]
    public async Task SaveChanges_ExplicitInterfaceImpl_ShouldSyncIsPublished()
    {
        // Arrange — entity uses explicit IWorkflowStateful implementation (like VersionedWorkflowEntity)
        using TestDbContext context = CreateContext();
        TestExplicitEntity entity = new()
        {
            Id = Guid.NewGuid(),
            LifecycleStatus = WorkflowLifecycleStatus.Published,
            IsPublished = false, // Intentionally wrong — interceptor should fix
        };
        context.ExplicitEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.IsPublished.ShouldBeTrue();
    }

    [Fact]
    public async Task SaveChanges_ExplicitInterfaceImpl_ShouldCreateTransitionRecord()
    {
        // Arrange
        using TestDbContext context = CreateContext();
        TestExplicitEntity entity = new()
        {
            Id = Guid.NewGuid(),
            LifecycleStatus = WorkflowLifecycleStatus.Draft,
            IsPublished = false,
        };
        context.ExplicitEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act — change status
        entity.LifecycleStatus = WorkflowLifecycleStatus.Published;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        List<WorkflowTransitionRecord> records = await context.WorkflowTransitionRecords
            .ToListAsync(TestContext.Current.CancellationToken);

        records.Count.ShouldBe(1);
        WorkflowTransitionRecord record = records[0];
        record.EntityType.ShouldBe("TestExplicit");
        record.PreviousState.ShouldBe("Draft");
        record.NewState.ShouldBe("Published");

        entity.DomainEvents.ShouldHaveSingleItem();
        WorkflowStateChangedEvent stateChangedEvent = entity.DomainEvents.OfType<WorkflowStateChangedEvent>().Single();
        stateChangedEvent.EntityType.ShouldBe("TestExplicit");
        stateChangedEvent.PreviousState.ShouldBe("Draft");
        stateChangedEvent.NewState.ShouldBe("Published");
    }

    // ========================================================================
    // Test infrastructure
    // ========================================================================

    private TestDbContext CreateContext()
    {
        WorkflowTransitionInterceptor interceptor = new(
            _currentUserService, _clock, _guidGenerator, _currentTenant);

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;

        return new TestDbContext(options);
    }

    // --- Test entities ---

    private sealed class TestWorkflowEntity : AggregateRoot, IWorkflowStateful
    {
        public string Name { get; set; } = string.Empty;
        public WorkflowLifecycleStatus Status { get; set; }

        public static string StatusPropertyName => nameof(Status);
        public static string WorkflowEntityType => "TestWorkflowEntity";
        public string GetWorkflowEntityId() => Id.ToString();

        public void RaiseWorkflowStateChangedEvent(string entityType, string previousState, string newState, string transitionedBy) =>
            AddDomainEvent(new WorkflowStateChangedEvent(entityType, GetWorkflowEntityId(), previousState, newState, transitionedBy));
    }

    private sealed class TestVersionedEntity : AggregateRoot, IVersionedEntity, IWorkflowStateful
    {
        public Guid VersionId { get; set; }
        public int Version { get; set; }
        public WorkflowLifecycleStatus LifecycleStatus { get; set; }
        public bool IsPublished { get; set; }

        public static string StatusPropertyName => nameof(LifecycleStatus);
        public static string WorkflowEntityType => "TestVersionedEntity";
        public string GetWorkflowEntityId() => Id.ToString();

        public void RaiseWorkflowStateChangedEvent(string entityType, string previousState, string newState, string transitionedBy) =>
            AddDomainEvent(new WorkflowStateChangedEvent(entityType, GetWorkflowEntityId(), previousState, newState, transitionedBy));
    }

    /// <summary>
    /// Uses explicit interface implementation for static abstract members,
    /// mirroring <see cref="VersionedWorkflowEntity"/>.
    /// </summary>
    private sealed class TestExplicitEntity : AggregateRoot, IPublishable, IWorkflowStateful
    {
        public WorkflowLifecycleStatus LifecycleStatus { get; set; }
        public bool IsPublished { get; set; }

        static string IWorkflowStateful.StatusPropertyName => nameof(LifecycleStatus);
        static string IWorkflowStateful.WorkflowEntityType => "TestExplicit";
        public string GetWorkflowEntityId() => Id.ToString();

        public void RaiseWorkflowStateChangedEvent(string entityType, string previousState, string newState, string transitionedBy) =>
            AddDomainEvent(new WorkflowStateChangedEvent(entityType, GetWorkflowEntityId(), previousState, newState, transitionedBy));
    }

    // --- Test DbContext ---

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options)
        : DbContext(options), IWorkflowDbContext
    {
        public DbSet<TestWorkflowEntity> Entities => Set<TestWorkflowEntity>();
        public DbSet<TestVersionedEntity> VersionedEntities => Set<TestVersionedEntity>();
        public DbSet<TestExplicitEntity> ExplicitEntities => Set<TestExplicitEntity>();
        public DbSet<WorkflowTransitionRecord> WorkflowTransitionRecords => Set<WorkflowTransitionRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TestWorkflowEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<TestVersionedEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<TestExplicitEntity>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<WorkflowTransitionRecord>(b =>
            {
                b.HasKey(e => e.Id);
                b.Property(e => e.Id).ValueGeneratedNever();
            });
        }
    }
}
