using Granit.AuditLog.Abstractions;
using Granit.AuditLog.Domain;
using Granit.AuditLog.EntityFrameworkCore.Internal;
using Granit.AuditLog.EntityFrameworkCore.Internal.Services;
using Granit.AuditLog.Options;
using Granit.Core.MultiTenancy;
using Granit.Querying;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // Internal EF Core API usage — required to test internal DbContext

namespace Granit.AuditLog.EntityFrameworkCore.Tests.Internal;

public sealed class EfCoreAuditLogReaderFilterTests : IDisposable
{
    private readonly DbContextOptions<AuditLogDbContext> _dbOptions;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IOptions<AuditLogOptions> _options = Microsoft.Extensions.Options.Options.Create(new AuditLogOptions());

    public EfCoreAuditLogReaderFilterTests()
    {
        _dbOptions = new DbContextOptionsBuilder<AuditLogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetPagedAsync_FilterByUserId_ReturnsOnlyMatching()
    {
        await SeedEntriesAsync(
            CreateEntry("user-1", AuditLogCategory.DataMutation),
            CreateEntry("user-2", AuditLogCategory.DataMutation),
            CreateEntry("user-1", AuditLogCategory.DataMutation));

        EfCoreAuditLogReader reader = CreateReader();

        PagedResult<AuditLogEntry> result = await reader.GetPagedAsync(
            new AuditLogQuery(UserId: "user-1"),
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.Items.ShouldAllBe(e => e.UserId == "user-1");
    }

    [Fact]
    public async Task GetPagedAsync_FilterByCategory_ReturnsOnlyMatching()
    {
        await SeedEntriesAsync(
            CreateEntry("user-1", AuditLogCategory.DataMutation),
            CreateEntry("user-1", AuditLogCategory.ConfigurationChange),
            CreateEntry("user-1", AuditLogCategory.DataAccess));

        EfCoreAuditLogReader reader = CreateReader();

        PagedResult<AuditLogEntry> result = await reader.GetPagedAsync(
            new AuditLogQuery(Category: AuditLogCategory.ConfigurationChange),
            TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
        result.Items[0].Category.ShouldBe(AuditLogCategory.ConfigurationChange);
    }

    [Fact]
    public async Task GetPagedAsync_FilterByDateRange_ReturnsOnlyMatching()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await SeedEntriesAsync(
            CreateEntry("user-1", AuditLogCategory.DataMutation, now.AddDays(-5)),
            CreateEntry("user-1", AuditLogCategory.DataMutation, now.AddDays(-2)),
            CreateEntry("user-1", AuditLogCategory.DataMutation, now));

        EfCoreAuditLogReader reader = CreateReader();

        PagedResult<AuditLogEntry> result = await reader.GetPagedAsync(
            new AuditLogQuery(From: now.AddDays(-3), To: now.AddDays(-1)),
            TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetPagedAsync_FilterByEntityType_ReturnsOnlyMatching()
    {
        var entryId1 = Guid.NewGuid();
        var entryId2 = Guid.NewGuid();

        await using (AuditLogDbContext ctx = new(_dbOptions))
        {
            AuditLogEntry entry1 = CreateEntry("user-1", AuditLogCategory.DataMutation);
            entry1.Id = entryId1;
            entry1.EntityChanges.Add(new AuditEntityChange
            {
                Id = Guid.NewGuid(),
                AuditLogEntryId = entryId1,
                EntityType = "Patient",
                EntityId = "1",
                ChangeType = AuditChangeType.Created,
            });

            AuditLogEntry entry2 = CreateEntry("user-1", AuditLogCategory.DataMutation);
            entry2.Id = entryId2;
            entry2.EntityChanges.Add(new AuditEntityChange
            {
                Id = Guid.NewGuid(),
                AuditLogEntryId = entryId2,
                EntityType = "Invoice",
                EntityId = "2",
                ChangeType = AuditChangeType.Modified,
            });

            ctx.AuditLogEntries.AddRange(entry1, entry2);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        EfCoreAuditLogReader reader = CreateReader();

        PagedResult<AuditLogEntry> result = await reader.GetPagedAsync(
            new AuditLogQuery(EntityType: "Patient"),
            TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task GetPagedAsync_Pagination_ReturnsCorrectPage()
    {
        List<AuditLogEntry> entries = [];
        for (int i = 0; i < 5; i++)
        {
            entries.Add(CreateEntry("user-1", AuditLogCategory.DataMutation, DateTimeOffset.UtcNow.AddMinutes(-i)));
        }

        await SeedEntriesAsync([.. entries]);

        EfCoreAuditLogReader reader = CreateReader();

        PagedResult<AuditLogEntry> page1 = await reader.GetPagedAsync(
            new AuditLogQuery(Page: 1, PageSize: 2),
            TestContext.Current.CancellationToken);

        PagedResult<AuditLogEntry> page2 = await reader.GetPagedAsync(
            new AuditLogQuery(Page: 2, PageSize: 2),
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
            CreateEntry("user-1", AuditLogCategory.DataMutation),
            CreateEntry("user-1", AuditLogCategory.DataMutation));

        EfCoreAuditLogReader reader = CreateReader();

        PagedResult<AuditLogEntry> result = await reader.GetPagedAsync(
            new AuditLogQuery(Page: 1, PageSize: 10),
            TestContext.Current.CancellationToken);

        result.HasMore.ShouldBeFalse();
    }

    [Fact]
    public async Task GetPagedAsync_NoFilters_ReturnsAll()
    {
        await SeedEntriesAsync(
            CreateEntry("user-1", AuditLogCategory.DataMutation),
            CreateEntry("user-2", AuditLogCategory.ConfigurationChange));

        EfCoreAuditLogReader reader = CreateReader();

        PagedResult<AuditLogEntry> result = await reader.GetPagedAsync(
            new AuditLogQuery(),
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetPagedAsync_OrderedByTimestampDescending()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        await SeedEntriesAsync(
            CreateEntry("user-1", AuditLogCategory.DataMutation, now.AddHours(-2)),
            CreateEntry("user-1", AuditLogCategory.DataMutation, now),
            CreateEntry("user-1", AuditLogCategory.DataMutation, now.AddHours(-1)));

        EfCoreAuditLogReader reader = CreateReader();

        PagedResult<AuditLogEntry> result = await reader.GetPagedAsync(
            new AuditLogQuery(),
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

        await using (AuditLogDbContext ctx = new(_dbOptions))
        {
            foreach (Guid id in new[] { entryId1, entryId2, entryId3 })
            {
                AuditLogEntry entry = CreateEntry("user-1", AuditLogCategory.DataMutation);
                entry.Id = id;
                entry.EntityChanges.Add(new AuditEntityChange
                {
                    Id = Guid.NewGuid(),
                    AuditLogEntryId = id,
                    EntityType = "Patient",
                    EntityId = "42",
                    ChangeType = AuditChangeType.Modified,
                });
                ctx.AuditLogEntries.Add(entry);
            }

            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        EfCoreAuditLogReader reader = CreateReader();

        PagedResult<AuditLogEntry> page1 = await reader.GetByEntityAsync(
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

        await using (AuditLogDbContext ctx = new(_dbOptions))
        {
            AuditLogEntry entry1 = CreateEntry("user-1", AuditLogCategory.DataMutation);
            entry1.Id = entryId1;
            entry1.EntityChanges.Add(new AuditEntityChange
            {
                Id = Guid.NewGuid(),
                AuditLogEntryId = entryId1,
                EntityType = "Patient",
                EntityId = "42",
                ChangeType = AuditChangeType.Created,
            });

            AuditLogEntry entry2 = CreateEntry("user-1", AuditLogCategory.DataMutation);
            entry2.Id = entryId2;
            entry2.EntityChanges.Add(new AuditEntityChange
            {
                Id = Guid.NewGuid(),
                AuditLogEntryId = entryId2,
                EntityType = "Patient",
                EntityId = "99",
                ChangeType = AuditChangeType.Modified,
            });

            ctx.AuditLogEntries.AddRange(entry1, entry2);
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        EfCoreAuditLogReader reader = CreateReader();

        PagedResult<AuditLogEntry> result = await reader.GetPagedAsync(
            new AuditLogQuery(EntityId: "42"),
            TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
    }

    // --- Helpers ---

    private EfCoreAuditLogReader CreateReader()
    {
        IDbContextFactory<AuditLogDbContext> factory = new TestDbContextFactory(_dbOptions);
        return new EfCoreAuditLogReader(factory, _cache, _currentTenant, _options);
    }

    private async Task SeedEntriesAsync(params AuditLogEntry[] entries)
    {
        await using AuditLogDbContext ctx = new(_dbOptions);
        ctx.AuditLogEntries.AddRange(entries);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static AuditLogEntry CreateEntry(
        string userId,
        AuditLogCategory category,
        DateTimeOffset? timestamp = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            Timestamp = timestamp ?? DateTimeOffset.UtcNow,
            UserId = userId,
            Category = category,
        };

    private sealed class TestDbContextFactory(DbContextOptions<AuditLogDbContext> options)
        : IDbContextFactory<AuditLogDbContext>
    {
        public AuditLogDbContext CreateDbContext() => new(options);
    }
}
