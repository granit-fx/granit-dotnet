using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Options;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class EfCoreAuditingReaderCachingTests : IDisposable
{
    private readonly DbContextOptions<AuditingHostDbContext> _dbOptions;
    private readonly FusionCache _cache = new(new FusionCacheOptions());
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IOptions<AuditingOptions> _options = Microsoft.Extensions.Options.Options.Create(new AuditingOptions());

    public EfCoreAuditingReaderCachingTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AuditingHostDbContext>()
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

        IDbContextFactory<AuditingHostDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(new AuditingContextResolver(Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared, factory), _cache, _currentTenant, StubEmptyTenantsAccessor(), [], _options);

        // Act — first call loads from DB
        AuditEntry? first = await reader.GetByIdAsync(entryId, TestContext.Current.CancellationToken);
        first.ShouldNotBeNull();

        // Delete from DB to prove second call comes from cache
        await using (AuditingHostDbContext ctx = new(_dbOptions, GranitDesignTime.CurrentTenant))
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
        IDbContextFactory<AuditingHostDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(new AuditingContextResolver(Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared, factory), _cache, _currentTenant, StubEmptyTenantsAccessor(), [], _options);

        var missingId = Guid.NewGuid();

        // Act
        AuditEntry? result = await reader.GetByIdAsync(missingId, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
        MaybeValue<AuditEntry?> maybe = await _cache.TryGetAsync<AuditEntry?>($"audit:global:entry:{missingId}", token: TestContext.Current.CancellationToken);
        maybe.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task GetByEntityAsync_SecondCall_ReturnsCachedResult()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        await SeedEntryWithEntityChangeAsync(entryId, "Invoice", "INV-001");

        IDbContextFactory<AuditingHostDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(new AuditingContextResolver(Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared, factory), _cache, _currentTenant, StubEmptyTenantsAccessor(), [], _options);

        // Act — first call loads from DB
        PagedResult<AuditEntry> first = await reader.GetByEntityAsync("Invoice", "INV-001", cancellationToken: TestContext.Current.CancellationToken);
        first.Items.Count.ShouldBe(1);

        // Delete from DB to prove second call comes from cache
        await using (AuditingHostDbContext ctx = new(_dbOptions, GranitDesignTime.CurrentTenant))
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
    public async Task GetByEntityAsync_WithColonsInParameters_DoesNotCollideCacheKeys()
    {
        // Arrange — two entries with entity parameters that would collide without encoding
        var entryId1 = Guid.NewGuid();
        var entryId2 = Guid.NewGuid();
        await SeedEntryWithEntityChangeAsync(entryId1, "A:B", "C");
        await SeedEntryWithEntityChangeAsync(entryId2, "A", "B:C");

        IDbContextFactory<AuditingHostDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(new AuditingContextResolver(Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared, factory), _cache, _currentTenant, StubEmptyTenantsAccessor(), [], _options);

        // Act
        PagedResult<AuditEntry> result1 = await reader.GetByEntityAsync("A:B", "C", cancellationToken: TestContext.Current.CancellationToken);
        PagedResult<AuditEntry> result2 = await reader.GetByEntityAsync("A", "B:C", cancellationToken: TestContext.Current.CancellationToken);

        // Assert — each query returns its own entry, not the other's cached result
        result1.Items.ShouldAllBe(e => e.EntityChanges.Any(ec => ec.EntityType == "A:B"));
        result2.Items.ShouldAllBe(e => e.EntityChanges.Any(ec => ec.EntityType == "A"));
    }

    // -------------------------------------------------------------------------
    // Alias resolution — ADR-051 split persistence
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetByEntityAsync_WithAliasProvider_ReturnsRowsStampedWithAliasedClrName()
    {
        // Arrange — audit rows are stamped "LocalIdentity" (CLR name), but a
        // reader query for "User" must surface them because of the alias.
        const string SharedId = "user-42";
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "LocalIdentity", SharedId);
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "FederatedIdentity", SharedId);
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "User", SharedId);

        StaticAuditEntityTypeAliasProvider aliasProvider = new(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["User"] = new HashSet<string>(StringComparer.Ordinal) { "LocalIdentity", "FederatedIdentity" },
            });

        IDbContextFactory<AuditingHostDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(new AuditingContextResolver(Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared, factory), _cache, _currentTenant, StubEmptyTenantsAccessor(), [aliasProvider], _options);

        // Act
        PagedResult<AuditEntry> result = await reader
            .GetByEntityAsync("User", SharedId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert — all three rows surface under the canonical name.
        result.Items.Count.ShouldBe(3);
        result.Items.SelectMany(e => e.EntityChanges)
            .Select(c => c.EntityType)
            .ShouldBe(new[] { "User", "LocalIdentity", "FederatedIdentity" }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetByEntityAsync_QueryByPhysicalAlias_DoesNotIncludeCanonicalRows()
    {
        // Arrange — directional alias: a lookup by "LocalIdentity" must NOT
        // bleed in "User" rows. Forensic precision is preserved.
        const string SharedId = "user-42";
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "LocalIdentity", SharedId);
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "User", SharedId);

        StaticAuditEntityTypeAliasProvider aliasProvider = new(
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["User"] = new HashSet<string>(StringComparer.Ordinal) { "LocalIdentity" },
            });

        IDbContextFactory<AuditingHostDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(new AuditingContextResolver(Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared, factory), _cache, _currentTenant, StubEmptyTenantsAccessor(), [aliasProvider], _options);

        // Act
        PagedResult<AuditEntry> result = await reader
            .GetByEntityAsync("LocalIdentity", SharedId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert — only the LocalIdentity row.
        result.Items.Count.ShouldBe(1);
        result.Items.Single().EntityChanges.Single().EntityType.ShouldBe("LocalIdentity");
    }

    [Fact]
    public async Task GetByEntityAsync_WithNoProviders_BehavesAsBefore()
    {
        // Arrange — hosts without any IAuditEntityTypeAliasProvider see the
        // pre-aliasing behaviour: strict ordinal match on EntityType.
        const string SharedId = "user-42";
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "LocalIdentity", SharedId);
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "User", SharedId);

        IDbContextFactory<AuditingHostDbContext> factory = new TestDbContextFactory(_dbOptions);
        EfCoreAuditingReader reader = new(new AuditingContextResolver(Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared, factory), _cache, _currentTenant, StubEmptyTenantsAccessor(), [], _options);

        // Act
        PagedResult<AuditEntry> result = await reader
            .GetByEntityAsync("User", SharedId, cancellationToken: TestContext.Current.CancellationToken);

        // Assert — only the canonical row, the LocalIdentity row stays hidden.
        result.Items.Count.ShouldBe(1);
        result.Items.Single().EntityChanges.Single().EntityType.ShouldBe("User");
    }

    // --- Helpers ---

    private async Task SeedEntryAsync(Guid id)
    {
        await using AuditingHostDbContext ctx = new(_dbOptions, GranitDesignTime.CurrentTenant);
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
        await using AuditingHostDbContext ctx = new(_dbOptions, GranitDesignTime.CurrentTenant);
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
    private sealed class TestDbContextFactory(DbContextOptions<AuditingHostDbContext> options)
        : IDbContextFactory<AuditingHostDbContext>
    {
        public AuditingHostDbContext CreateDbContext() => new(options, GranitDesignTime.CurrentTenant);
    }

    private static ITenantsAccessor StubEmptyTenantsAccessor()
    {
        ITenantsAccessor accessor = Substitute.For<ITenantsAccessor>();
        accessor.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(Guid Id, string Name)>>([]));
        return accessor;
    }
}
