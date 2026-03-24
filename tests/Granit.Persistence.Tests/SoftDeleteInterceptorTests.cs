// =============================================================================
// Tests - SoftDeleteInterceptor
// =============================================================================
// Verifies that physical deletion is converted to logical deletion
// for ISoftDeletable entities (GDPR compliance).
//
// Approach: the interceptor is registered in the DbContext and
// SaveChangesAsync is called directly. IClock is mocked for exact assertions.
// =============================================================================

using Granit.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.Interceptors;
using Granit.Timing;
using Granit.Users;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests;

public sealed class SoftDeleteInterceptorTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly ICurrentUserService _currentUserService;
    private readonly IClock _clock;

    public SoftDeleteInterceptorTests()
    {
        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.UserId.Returns("user-test-123");

        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(FixedNow);
    }

    [Fact]
    public async Task SaveChangesAsync_OnDelete_ConvertToSoftDelete()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        var entity = new TestSoftDeletableEntity
        {
            Id = Guid.NewGuid(),
            Name = "ToDelete",
            CreatedAt = FixedNow.AddDays(-1),
            CreatedBy = "user-test-123"
        };
        context.Entities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Delete the entity
        context.Entities.Remove(entity);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — the entity is soft-deleted (not physically removed)
        entity.IsDeleted.ShouldBeTrue();
        entity.DeletedAt.ShouldBe(FixedNow);
        entity.DeletedBy.ShouldBe("user-test-123");

        // Verify the entity still exists in the database (not physically deleted)
        int count = await context.Entities.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task SaveChangesAsync_OnModify_DoesNotTriggerSoftDelete()
    {
        // Arrange
        await using TestDbContext context = CreateContext();
        var entity = new TestSoftDeletableEntity
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            CreatedAt = FixedNow.AddDays(-1),
            CreatedBy = "user-test-123"
        };
        context.Entities.Add(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Modify the entity (not delete)
        entity.Name = "Modified";

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert — no soft delete
        entity.IsDeleted.ShouldBeFalse();
        entity.DeletedAt.ShouldBeNull();
    }

    private TestDbContext CreateContext()
    {
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(false);
        var auditInterceptor = new AuditedEntityInterceptor(_currentUserService, _clock, guidGenerator, currentTenant);
        var softDeleteInterceptor = new SoftDeleteInterceptor(_currentUserService, _clock);
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .AddInterceptors(auditInterceptor, softDeleteInterceptor)
            .Options;
        return new TestDbContext(options);
    }

    private sealed class TestSoftDeletableEntity : AuditedEntity, ISoftDeletable
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedAt { get; set; }
        public string? DeletedBy { get; set; }
    }

    private sealed class TestDbContext(DbContextOptions<SoftDeleteInterceptorTests.TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestSoftDeletableEntity> Entities => Set<TestSoftDeletableEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<TestSoftDeletableEntity>().Property(e => e.Id).ValueGeneratedNever();
    }
}
