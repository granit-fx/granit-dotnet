// =============================================================================
// Tests - SoftDeleteInterceptor
// =============================================================================
// Verifies that physical deletion is converted to logical deletion for
// ISoftDeletable entities (GDPR compliance) — on SQLite (relational), with the
// real named soft-delete filter from ApplyGranitConventions, so both the
// UPDATE-instead-of-DELETE conversion and the filter's SQL behavior are proven
// (the InMemory provider fakes both).
// =============================================================================

using Granit.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Granit.Timing;
using Granit.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests;

public sealed class SoftDeleteInterceptorTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly ICurrentUserService _currentUserService;
    private readonly IClock _clock;

    public SoftDeleteInterceptorTests()
    {
        _connection.Open();

        _currentUserService = Substitute.For<ICurrentUserService>();
        _currentUserService.UserId.Returns("user-test-123");

        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(FixedNow);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task SaveChangesAsync_OnDelete_ConvertToSoftDelete()
    {
        await using TestDbContext context = CreateContext();
        TestSoftDeletableEntity entity = AddEntity(context, "ToDelete");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.Entities.Remove(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.IsDeleted.ShouldBeTrue();
        entity.DeletedAt.ShouldBe(FixedNow);
        entity.DeletedBy.ShouldBe("user-test-123");
    }

    [Fact]
    public async Task SaveChangesAsync_OnDelete_RowPhysicallySurvives()
    {
        await using TestDbContext context = CreateContext();
        TestSoftDeletableEntity entity = AddEntity(context, "Survivor");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.Entities.Remove(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Raw count outside EF filters: the DELETE became an UPDATE, the row is still there.
        int rawCount = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM \"Entities\"")
            .SingleAsync(TestContext.Current.CancellationToken);
        rawCount.ShouldBe(1);
    }

    [Fact]
    public async Task NamedSoftDeleteFilter_ExcludesSoftDeletedRows_FromStandardQueries()
    {
        await using TestDbContext context = CreateContext();
        TestSoftDeletableEntity entity = AddEntity(context, "Filtered");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.Entities.Remove(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        (await context.Entities.CountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(0, "the named soft-delete filter must hide the tombstone");
    }

    [Fact]
    public async Task NamedSoftDeleteFilter_PerQueryBypass_RevealsTombstone()
    {
        await using TestDbContext context = CreateContext();
        TestSoftDeletableEntity entity = AddEntity(context, "Tombstone");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.Entities.Remove(entity);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        TestSoftDeletableEntity tombstone = await context.Entities
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete])
            .SingleAsync(TestContext.Current.CancellationToken);
        tombstone.IsDeleted.ShouldBeTrue();
        tombstone.DeletedAt.ShouldBe(FixedNow);
        tombstone.DeletedBy.ShouldBe("user-test-123");
    }

    [Fact]
    public async Task SaveChangesAsync_OnModify_DoesNotTriggerSoftDelete()
    {
        await using TestDbContext context = CreateContext();
        TestSoftDeletableEntity entity = AddEntity(context, "Original");
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.Name = "Modified";
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        entity.IsDeleted.ShouldBeFalse();
        entity.DeletedAt.ShouldBeNull();
    }

    private static TestSoftDeletableEntity AddEntity(TestDbContext context, string name)
    {
        TestSoftDeletableEntity entity = new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = FixedNow.AddDays(-1),
            CreatedBy = "user-test-123",
        };
        context.Entities.Add(entity);
        return entity;
    }

    private TestDbContext CreateContext()
    {
        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        ICurrentTenant currentTenant = Substitute.For<ICurrentTenant>();
        currentTenant.IsAvailable.Returns(false);
        var auditInterceptor = new AuditedEntityInterceptor(_currentUserService, _clock, guidGenerator, currentTenant);
        var softDeleteInterceptor = new SoftDeleteInterceptor(_currentUserService, _clock);
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(auditInterceptor, softDeleteInterceptor)
            .Options;
        TestDbContext context = new(options);
        context.Database.EnsureCreated();
        return context;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestSoftDeletableEntity>().Property(e => e.Id).ValueGeneratedNever();

            // Real named filters (soft-delete included) — the behavior under test.
            modelBuilder.ApplyGranitConventions();
        }
    }
}
