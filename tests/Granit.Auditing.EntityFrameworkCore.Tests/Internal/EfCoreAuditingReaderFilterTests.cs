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

public sealed class EfCoreAuditingReaderFilterTests : IDisposable
{
    private readonly DbContextOptions<AuditingDbContext> _dbOptions;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IOptions<AuditingOptions> _options = Microsoft.Extensions.Options.Options.Create(new AuditingOptions());

    public EfCoreAuditingReaderFilterTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AuditingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetPagedAsync_FilterByUserId_ReturnsOnlyMatching()
    {
        await SeedEntriesAsync(
            CreateEntry("user-1", AuditCategory.DataMutation),
            CreateEntry("user-2", AuditCategory.DataMutation),
            CreateEntry("user-1", AuditCategory.DataMutation));

        EfCoreAuditingReader reader = CreateReader();

        PagedResult<AuditEntry> result = await reader.GetPagedAsync(
            new AuditingQuery(UserId: "user-1"),
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.Items.ShouldAllBe(e => e.UserId == "user-1");
    }

    [Fact]
    public async Task GetPagedAsync_FilterByCategory_ReturnsOnlyMatching()
    {
        await SeedEntriesAsync(
            CreateEntry("user-1", AuditCategory.DataMutation),
            CreateEntry("user-1", AuditCategory.ConfigurationChange),
            CreateEntry("user-1", AuditCategory.DataAccess));

        EfCoreAuditingReader reader = CreateReader();

        PagedResult<AuditEntry> result = await reader.GetPagedAsync(
            new AuditingQuery(Category: AuditCategory.ConfigurationChange),
            TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
        result.Items[0].Category.ShouldBe(AuditCategory.ConfigurationChange);
    }

    [Fact]
    public async Task GetPagedAsync_FilterByDateRange_ReturnsOnlyMatching()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await SeedEntriesAsync(
            CreateEntry("user-1", AuditCategory.DataMutation, now.AddDays(-5)),
            CreateEntry("user-1", AuditCategory.DataMutation, now.AddDays(-2)),
            CreateEntry("user-1", AuditCategory.DataMutation, now));

        EfCoreAuditingReader reader = CreateReader();

        PagedResult<AuditEntry> result = await reader.GetPagedAsync(
            new AuditingQuery(From: now.AddDays(-3), To: now.AddDays(-1)),
            TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetPagedAsync_FilterByEntityType_ReturnsOnlyMatching()
    {
        var entryId1 = Guid.NewGuid();
        var entryId2 = Guid.NewGuid();

        await using (AuditingDbContext ctx = new(_dbOptions))
        {
            AuditEntry entry1 = CreateEntry("user-1", AuditCategory.DataMutation);
            entry1.Id = entryId1;
            entry1.EntityChanges.Add(new AuditEntityChange
            {
                Id = Guid.NewGuid(),
                AuditEntryId = entryId1,
                EntityType = "Patient",
                EntityId = "1",
                ChangeType = AuditChangeType.Created,
            });

            AuditEntry entry2 = CreateEntry("user-1", AuditCategory.DataMutation);
            entry2.Id = entryId2;
            entry2.EntityChanges.Add(new AuditEntityChange
            {
                Id = Guid.NewGuid(),
                AuditEntryId = entryId2,
                EntityType = "Invoice",
                EntityId = "2",
                ChangeType = AuditChangeType.Modified,
            });

            ctx.AuditEntries.AddRange(entry1, entry2);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        EfCoreAuditingReader reader = CreateReader();

        PagedResult<AuditEntry> result = await reader.GetPagedAsync(
            new AuditingQuery(EntityType: "Patient"),
            TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetPagedAsync_Pagination_ReturnsCorrectPage()
    {
        List<AuditEntry> entries = [];
        for (int i = 0; i < 5; i++)
        {
            entries.Add(CreateEntry("user-1", AuditCategory.DataMutation, DateTimeOffset.UtcNow.AddMinutes(-i)));
        }

        await SeedEntriesAsync([.. entries]);

        EfCoreAuditingReader reader = CreateReader();

        PagedResult<AuditEntry> page1 = await reader.GetPagedAsync(
            new AuditingQuery(Page: 1, PageSize: 2),
            TestContext.Current.CancellationToken);

        PagedResult<AuditEntry> page2 = await reader.GetPagedAsync(
            new AuditingQuery(Page: 2, PageSize: 2),
            TestContext.Current.CancellationToken);

        page1.Items.Count.ShouldBe(2);
        page1.TotalCount.ShouldBe(5);
        page1.HasMore.ShouldBeTrue();

        page2.Items.Count.ShouldBe(2);
        page2.TotalCount.ShouldBe(5);
        page2.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_LastPage_HasMoreIsFalse()
    {
        await SeedEntriesAsync(
            CreateEntry("user-1", AuditCategory.DataMutation),
            CreateEntry("user-1", AuditCategory.DataMutation));

        EfCoreAuditingReader reader = CreateReader();

        PagedResult<AuditEntry> result = await reader.GetPagedAsync(
            new AuditingQuery(Page: 1, PageSize: 10),
            TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_NoFilters_ReturnsAll()
    {
        await SeedEntriesAsync(
            CreateEntry("user-1", AuditCategory.DataMutation),
            CreateEntry("user-2", AuditCategory.ConfigurationChange));

        EfCoreAuditingReader reader = CreateReader();

        PagedResult<AuditEntry> result = await reader.GetPagedAsync(
            new AuditingQuery(),
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedAsync_OrderedByTimestampDescending()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await SeedEntriesAsync(
            CreateEntry("user-1", AuditCategory.DataMutation, now.AddHours(-2)),
            CreateEntry("user-1", AuditCategory.DataMutation, now),
            CreateEntry("user-1", AuditCategory.DataMutation, now.AddHours(-1)));

        EfCoreAuditingReader reader = CreateReader();

        PagedResult<AuditEntry> result = await reader.GetPagedAsync(
            new AuditingQuery(),
            TestContext.Current.CancellationToken);

        result.Items[0].Timestamp.ShouldBeGreaterThanOrEqualTo(result.Items[1].Timestamp);
        result.Items[1].Timestamp.ShouldBeGreaterThanOrEqualTo(result.Items[2].Timestamp);
    }

    [Fact]
    public async Task GetByEntityAsync_Pagination_Works()
    {
        var entryId1 = Guid.NewGuid();
        var entryId2 = Guid.NewGuid();
        var entryId3 = Guid.NewGuid();

        await using (AuditingDbContext ctx = new(_dbOptions))
        {
            foreach (Guid id in new[] { entryId1, entryId2, entryId3 })
            {
                AuditEntry entry = CreateEntry("user-1", AuditCategory.DataMutation);
                entry.Id = id;
                entry.EntityChanges.Add(new AuditEntityChange
                {
                    Id = Guid.NewGuid(),
                    AuditEntryId = id,
                    EntityType = "Patient",
                    EntityId = "42",
                    ChangeType = AuditChangeType.Modified,
                });
                ctx.AuditEntries.Add(entry);
            }

            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        EfCoreAuditingReader reader = CreateReader();

        PagedResult<AuditEntry> page1 = await reader.GetByEntityAsync(
            "Patient", "42", page: 1, pageSize: 2,
            cancellationToken: TestContext.Current.CancellationToken);

        page1.Items.Count.ShouldBe(2);
        page1.TotalCount.ShouldBe(3);
        page1.HasMore.ShouldBeTrue();
    }

    [Fact]
    public async Task GetPagedAsync_FilterByEntityId_ReturnsOnlyMatching()
    {
        var entryId1 = Guid.NewGuid();
        var entryId2 = Guid.NewGuid();

        await using (AuditingDbContext ctx = new(_dbOptions))
        {
            AuditEntry entry1 = CreateEntry("user-1", AuditCategory.DataMutation);
            entry1.Id = entryId1;
            entry1.EntityChanges.Add(new AuditEntityChange
            {
                Id = Guid.NewGuid(),
                AuditEntryId = entryId1,
                EntityType = "Patient",
                EntityId = "42",
                ChangeType = AuditChangeType.Created,
            });

            AuditEntry entry2 = CreateEntry("user-1", AuditCategory.DataMutation);
            entry2.Id = entryId2;
            entry2.EntityChanges.Add(new AuditEntityChange
            {
                Id = Guid.NewGuid(),
                AuditEntryId = entryId2,
                EntityType = "Patient",
                EntityId = "99",
                ChangeType = AuditChangeType.Modified,
            });

            ctx.AuditEntries.AddRange(entry1, entry2);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        EfCoreAuditingReader reader = CreateReader();

        PagedResult<AuditEntry> result = await reader.GetPagedAsync(
            new AuditingQuery(EntityId: "42"),
            TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    // --- Helpers ---

    private EfCoreAuditingReader CreateReader()
    {
        IDbContextFactory<AuditingDbContext> factory = new TestDbContextFactory(_dbOptions);
        return new EfCoreAuditingReader(factory, _cache, _currentTenant, _options);
    }

    private async Task SeedEntriesAsync(params AuditEntry[] entries)
    {
        await using AuditingDbContext ctx = new(_dbOptions);
        ctx.AuditEntries.AddRange(entries);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static AuditEntry CreateEntry(
        string userId,
        AuditCategory category,
        DateTimeOffset? timestamp = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            UserId = userId,
            Category = category,
        };

    private sealed class TestDbContextFactory(DbContextOptions<AuditingDbContext> options)
        : IDbContextFactory<AuditingDbContext>
    {
        public AuditingDbContext CreateDbContext() => new(options);
    }
}
