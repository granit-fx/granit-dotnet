using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.AuditLog.EntityFrameworkCore.Internal;
using Granit.AuditLog.EntityFrameworkCore.Internal.Services;
using Granit.AuditLog.Options;
using Granit.MultiTenancy;
using Granit.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.AuditLog.EntityFrameworkCore.Tests.Internal;

public sealed class EfCoreAuditLogReaderCachingTests : IDisposable
{
    private readonly DbContextOptions<AuditLogDbContext> _dbOptions;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IOptions<AuditLogOptions> _options = Microsoft.Extensions.Options.Options.Create(new AuditLogOptions());

    public EfCoreAuditLogReaderCachingTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AuditLogDbContext>()
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

        IDbContextFactory<AuditLogDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditLogReader reader = new(factory, _cache, _currentTenant, _options);

        // Act — first call loads from DB
        AuditLogEntry? first = await reader.GetByIdAsync(entryId, TestContext.Current.CancellationToken);
        first.ShouldNotBeNull();

        // Delete from DB to prove second call comes from cache
        await using (AuditLogDbContext ctx = new(_dbOptions))
        {
            ctx.AuditLogEntries.RemoveRange(ctx.AuditLogEntries);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act — second call returns cached
        AuditLogEntry? second = await reader.GetByIdAsync(entryId, TestContext.Current.CancellationToken);

        // Assert
        second.ShouldNotBeNull();
        second.Id.ShouldBe(entryId);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_DoesNotCache()
    {
        // Arrange
        IDbContextFactory<AuditLogDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditLogReader reader = new(factory, _cache, _currentTenant, _options);

        var missingId = Guid.NewGuid();

        // Act
        AuditLogEntry? result = await reader.GetByIdAsync(missingId, TestContext.Current.CancellationToken);

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

        IDbContextFactory<AuditLogDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditLogReader reader = new(factory, _cache, _currentTenant, _options);

        // Act — first call loads from DB
        PagedResult<AuditLogEntry> first = await reader.GetByEntityAsync("Invoice", "INV-001", cancellationToken: TestContext.Current.CancellationToken);
        first.Items.Count.ShouldBe(1);

        // Delete from DB to prove second call comes from cache
        await using (AuditLogDbContext ctx = new(_dbOptions))
        {
            ctx.AuditLogEntries.RemoveRange(ctx.AuditLogEntries);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Act — second call returns cached
        PagedResult<AuditLogEntry> second = await reader.GetByEntityAsync("Invoice", "INV-001", cancellationToken: TestContext.Current.CancellationToken);

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

        IDbContextFactory<AuditLogDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditLogReader reader = new(factory, _cache, _currentTenant, _options);

        // Act
        PagedResult<AuditLogEntry> result = await reader.GetPagedAsync(
            new AuditLogQuery { Page = 1, PageSize = 10 },
            TestContext.Current.CancellationToken);

        // Assert — verify no cache entries were created for paged queries
        result.Items.Count.ShouldBe(1);
        _cache.Count.ShouldBe(0);
    }

    // --- Helpers ---

    private async Task SeedEntryAsync(Guid id)
    {
        await using AuditLogDbContext ctx = new(_dbOptions);
        ctx.AuditLogEntries.Add(new AuditLogEntry
        {
            Id = id,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditLogCategory.DataMutation,
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedEntryWithEntityChangeAsync(Guid entryId, string entityType, string entityId)
    {
        await using AuditLogDbContext ctx = new(_dbOptions);
        var entry = new AuditLogEntry
        {
            Id = entryId,
            Timestamp = DateTimeOffset.UtcNow,
            UserId = "user-1",
            Category = AuditLogCategory.DataMutation,
        };
        entry.EntityChanges.Add(new AuditEntityChange
        {
            Id = Guid.NewGuid(),
            AuditLogEntryId = entryId,
            EntityType = entityType,
            EntityId = entityId,
            ChangeType = AuditChangeType.Created,
        });
        ctx.AuditLogEntries.Add(entry);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Minimal IDbContextFactory for testing — creates contexts with the shared InMemory options.
    /// </summary>
    private sealed class TestDbContextFactory(DbContextOptions<AuditLogDbContext> options)
        : IDbContextFactory<AuditLogDbContext>
    {
        public AuditLogDbContext CreateDbContext() => new(options);
    }
}
