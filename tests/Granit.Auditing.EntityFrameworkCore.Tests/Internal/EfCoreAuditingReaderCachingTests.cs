using Granit.Auditing.Abstractions;
using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Options;
using Granit.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class EfCoreAuditingReaderCachingTests : IDisposable
{
    private readonly DbContextOptions<AuditingDbContext> _dbOptions;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IOptions<AuditingOptions> _options = Microsoft.Extensions.Options.Options.Create(new AuditingOptions());

    public EfCoreAuditingReaderCachingTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetByIdAsync_SecondCall_ReturnsCachedEntry()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        await SeedEntryAsync(entryId);

        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(factory, _cache, _currentTenant, _options);

        // Act — first call loads from DB
        AuditEntry? first = await reader.GetByIdAsync(entryId, TestContext.Current.CancellationToken);
        first.ShouldNotBeNull();

        // Delete from DB to prove second call comes from cache
        await using (AuditingDbContext ctx = new(_dbOptions))
        {
            ctx.AuditEntries.RemoveRange(ctx.AuditEntries);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act — second call returns cached
        AuditEntry? second = await reader.GetByIdAsync(entryId, TestContext.Current.CancellationToken);

        // Assert
        second.ShouldNotBeNull();
        second.Id.ShouldBe(entryId);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_DoesNotCache()
    {
        // Arrange
        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(factory, _cache, _currentTenant, _options);

        var missingId = Guid.NewGuid();

        // Act
        AuditEntry? result = await reader.GetByIdAsync(missingId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
        _cache.TryGetValue($"audit:global:entry:{missingId}", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task GetByEntityAsync_SecondCall_ReturnsCachedResult()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        await SeedEntryWithEntityChangeAsync(entryId, "Invoice", "INV-001");

        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(factory, _cache, _currentTenant, _options);

        // Act — first call loads from DB
        PagedResult<AuditEntry> first = await reader.GetByEntityAsync("Invoice", "INV-001", cancellationToken: TestContext.Current.CancellationToken);
        first.Items.Count.ShouldBe(1);

        // Delete from DB to prove second call comes from cache
        await using (AuditingDbContext ctx = new(_dbOptions))
        {
            ctx.AuditEntries.RemoveRange(ctx.AuditEntries);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act — second call returns cached
        PagedResult<AuditEntry> second = await reader.GetByEntityAsync("Invoice", "INV-001", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        second.Items.Count.ShouldBe(1);
        second.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetPagedAsync_IsNotCached()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        await SeedEntryAsync(entryId);

        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(factory, _cache, _currentTenant, _options);

        // Act
        PagedResult<AuditEntry> result = await reader.GetPagedAsync(
            new AuditingQuery { Page = 1, PageSize = 10 },
            TestContext.Current.CancellationToken);

        // Assert — verify no cache entries were created for paged queries
        result.Items.Count.ShouldBe(1);
        _cache.Count.ShouldBe(0);
    }

    // --- Helpers ---

    private async Task SeedEntryAsync(Guid id)
    {
        await using AuditingDbContext ctx = new(_dbOptions);
        ctx.AuditEntries.Add(new AuditEntry
        {
            Id = id,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.DataMutation,
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedEntryWithEntityChangeAsync(Guid entryId, string entityType, string entityId)
    {
        await using AuditingDbContext ctx = new(_dbOptions);
        var entry = new AuditEntry
        {
            Id = entryId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditCategory.DataMutation,
        };
        entry.EntityChanges.Add(new AuditEntityChange
        {
            Id = Guid.NewGuid(),
            AuditEntryId = entryId,
            EntityType = entityType,
            EntityId = entityId,
            ChangeType = AuditChangeType.Created,
        });
        ctx.AuditEntries.Add(entry);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Minimal IDbContextFactory for testing — creates contexts with the shared InMemory options.
    /// </summary>
    private sealed class TestDbContextFactory(DbContextOptions<AuditingDbContext> options)
        : IDbContextFactory<AuditingDbContext>
    {
        public AuditingDbContext CreateDbContext() => new(options);
    }
}
