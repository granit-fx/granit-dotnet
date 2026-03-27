// =============================================================================
// Tests - AuditedEntityInterceptor
// =============================================================================
// Verifies that ISO 27001 audit fields are correctly populated
// when entities are created and modified.
//
// Approach: the interceptor is registered in the DbContext and
// SaveChangesAsync is called directly, which triggers the interceptor naturally.
// IClock is mocked for exact assertions (no BeCloseTo).
// =============================================================================

using System.Diagnostics.Metrics;
using Granit.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.Diagnostics;
using Granit.Persistence.Interceptors;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests;

public sealed class AuditableEntityInterceptorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);
    private static readonly Guid FixedGuid = Guid.Parse("12345678-1234-1234-1234-123456789abc");

    private readonly ICurrentUserService _currentUserService;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;
    private readonly PersistenceMetrics _metrics;

    public AuditableEntityInterceptorTests()
    {
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.UserId.Returns("user-test-123");

        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(FixedNow);

        _guidGenerator = Substitute.For<IGuidGenerator>();
        _guidGenerator.Create().Returns(FixedGuid);

        _currentTenant = Substitute.For<ICurrentTenant>();
        _currentTenant.IsAvailable.Returns(false);

        IMeterFactory meterFactory = Substitute.For<IMeterFactory>();
        meterFactory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        _metrics = new PersistenceMetrics(meterFactory);
    }

    [Fact]
    public async Task SaveChangesAsync_OnAdd_SetsCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        var entity = new TestEntity { Name = "Test" };
        context.TestEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.CreatedAt.ShouldBe(FixedNow);
        entity.CreatedBy.ShouldBe("user-test-123");
        entity.Id.ShouldBe(FixedGuid);
    }

    [Fact]
    public async Task SaveChangesAsync_OnModify_SetsModifiedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        var entity = new TestEntity
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            CreatedAt = FixedNow.AddDays(-1),
            CreatedBy = "original-user"
        };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Modify the entity
        entity.Name = "Modified";
        context.Entry(entity).State = EntityState.Modified;

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.ModifiedAt.ShouldBe(FixedNow);
        entity.ModifiedBy.ShouldBe("user-test-123");
    }

    [Fact]
    public async Task SaveChangesAsync_OnModify_DoesNotOverwriteCreatedFields()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        var entity = new TestEntity
        {
            Id = Guid.NewGuid(),
            Name = "Original"
        };
        context.TestEntities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Capture the creation values set by the interceptor during Add
        DateTimeOffset originalCreatedAt = entity.CreatedAt;
        string originalCreatedBy = entity.CreatedBy;

        // Advance time for the Modify
        _clock.Now.Returns(FixedNow.AddHours(1));

        // Modify the entity
        entity.Name = "Modified";
        context.Entry(entity).State = EntityState.Modified;

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — creation fields must not be overwritten
        entity.CreatedAt.ShouldBe(originalCreatedAt);
        entity.CreatedBy.ShouldBe(originalCreatedBy);
        // But ModifiedAt must reflect the new time
        entity.ModifiedAt.ShouldBe(FixedNow.AddHours(1));
    }

    [Fact]
    public async Task SaveChangesAsync_WithoutUser_UsesSystem()
    {
        // Arrange
        _currentUserService.UserId.Returns((string?)null);
        await using TestDbContext context = CreateContext();
        var entity = new TestEntity { Name = "Test" };
        context.TestEntities.Add(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        entity.CreatedBy.ShouldBe("system");
    }

    private TestDbContext CreateContext()
    {
        var interceptor = new AuditedEntityInterceptor(_currentUserService, _clock, _guidGenerator, _currentTenant, _metrics);
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(interceptor)
            .Options;
        return new TestDbContext(options);
    }

    private sealed class TestEntity : AuditedEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class TestDbContext(DbContextOptions<AuditableEntityInterceptorTests.TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            // ValueGeneratedNever: the interceptor handles GUID generation
            modelBuilder.Entity<TestEntity>().Property(e => e.Id).ValueGeneratedNever();
    }
}
