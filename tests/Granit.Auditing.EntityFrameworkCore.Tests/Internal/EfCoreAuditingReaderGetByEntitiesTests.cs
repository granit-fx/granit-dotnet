using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.Options;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

#pragma warning disable EF1001 // Internal EF Core API usage — required to construct AuditingDbContext directly.

namespace Granit.Auditing.EntityFrameworkCore.Tests.Internal;

public sealed class EfCoreAuditingReaderGetByEntitiesTests : IDisposable
{
    private readonly DbContextOptions<AuditingDbContext> _dbOptions;
    private readonly FusionCache _cache = new(new FusionCacheOptions());
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IOptions<AuditingOptions> _options = Microsoft.Extensions.Options.Options.Create(new AuditingOptions());

    public EfCoreAuditingReaderGetByEntitiesTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetByEntitiesAsync_NoTargets_ReturnsEmpty()
    {
        EfCoreAuditingReader reader = NewReader();

        IReadOnlyList<AuditEntry> result = await reader.GetByEntitiesAsync(
            [], limit: 10, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByEntitiesAsync_SingleTarget_BehavesLikeGetByEntity()
    {
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "Page", "page-1");
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "Page", "page-2");

        EfCoreAuditingReader reader = NewReader();

        IReadOnlyList<AuditEntry> result = await reader.GetByEntitiesAsync(
            [new AuditEntityRef("Page", "page-1")], limit: 10, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result.Single().EntityChanges.Single().EntityId.ShouldBe("page-1");
    }

    [Fact]
    public async Task GetByEntitiesAsync_MultipleTypesAndIds_UnionsTheirAudits()
    {
        // Parent + 2 child entity types — the typical aggregate shape.
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "Page", "page-1");
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "PageVersion", "v1");
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "PageVersion", "v2");
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "PageTranslation", "t-fr");
        // Decoy — a Page-2 row that must NOT leak in.
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "Page", "page-2");

        EfCoreAuditingReader reader = NewReader();

        AuditEntityRef[] targets =
        [
            new("Page", "page-1"),
            new("PageVersion", "v1"),
            new("PageVersion", "v2"),
            new("PageTranslation", "t-fr"),
        ];

        IReadOnlyList<AuditEntry> result = await reader.GetByEntitiesAsync(
            targets, limit: 10, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(4);
        result.SelectMany(e => e.EntityChanges).Select(c => c.EntityId)
            .ShouldBe(new[] { "page-1", "v1", "v2", "t-fr" }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetByEntitiesAsync_DoesNotCrossMatchPairsOfDifferentTypes()
    {
        // The OR-per-scope shape must keep (TypeA, IdB) from matching just
        // because TypeA exists in one scope and IdB in another.
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "Order", "shared-id");
        await SeedEntryWithEntityChangeAsync(Guid.NewGuid(), "Invoice", "other-id");

        EfCoreAuditingReader reader = NewReader();

        // We ask for (Order, other-id) and (Invoice, shared-id) — neither pair
        // exists in the audit log, even though both types and both ids appear
        // somewhere. A flattened-array implementation would have returned both.
        AuditEntityRef[] targets =
        [
            new("Order", "other-id"),
            new("Invoice", "shared-id"),
        ];

        IReadOnlyList<AuditEntry> result = await reader.GetByEntitiesAsync(
            targets, limit: 10, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByEntitiesAsync_OrdersByTimestampDescendingAndHonoursLimit()
    {
        // Oldest → newest.
        var oldId = Guid.NewGuid();
        var midId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        await SeedEntryWithEntityChangeAsync(oldId, "Page", "page-1", DateTimeOffset.UtcNow.AddMinutes(-10));
        await SeedEntryWithEntityChangeAsync(midId, "PageVersion", "v1", DateTimeOffset.UtcNow.AddMinutes(-5));
        await SeedEntryWithEntityChangeAsync(newId, "PageTranslation", "t-fr", DateTimeOffset.UtcNow);

        EfCoreAuditingReader reader = NewReader();

        AuditEntityRef[] targets =
        [
            new("Page", "page-1"),
            new("PageVersion", "v1"),
            new("PageTranslation", "t-fr"),
        ];

        IReadOnlyList<AuditEntry> result = await reader.GetByEntitiesAsync(
            targets, limit: 2, TestContext.Current.CancellationToken);

        result.Select(e => e.Id).ShouldBe([newId, midId]);
    }

    [Fact]
    public async Task GetByEntitiesAsync_NullTargets_Throws() =>
        await Should.ThrowAsync<ArgumentNullException>(() =>
            NewReader().GetByEntitiesAsync(null!, 10));

    [Fact]
    public async Task GetByEntitiesAsync_ZeroLimit_Throws() =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            NewReader().GetByEntitiesAsync([new AuditEntityRef("X", "1")], 0));

    [Fact]
    public async Task GetByEntitiesAsync_NegativeLimit_Throws() =>
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() =>
            NewReader().GetByEntitiesAsync([new AuditEntityRef("X", "1")], -1));

    // --- Helpers ---

    private EfCoreAuditingReader NewReader()
    {
        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(_dbOptions);
        return new EfCoreAuditingReader(factory, _cache, _currentTenant, [], _options);
    }

    private async Task SeedEntryWithEntityChangeAsync(
        Guid entryId, string entityType, string entityId, DateTimeOffset? timestamp = null)
    {
        await using AuditingDbContext ctx = new(_dbOptions, GranitDesignTime.CurrentTenant);
        var entry = new AuditEntry
        {
            Id = entryId,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
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

    private sealed class TestDbContextFactory(DbContextOptions<AuditingDbContext> options)
        : IDbContextFactory<AuditingDbContext>
    {
        public AuditingDbContext CreateDbContext() => new(options, GranitDesignTime.CurrentTenant);
    }
}
