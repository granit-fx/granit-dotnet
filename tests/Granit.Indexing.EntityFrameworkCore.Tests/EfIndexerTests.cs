using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Indexing.EntityFrameworkCore.Tests;

public sealed class EfIndexerTests
{
    [Fact]
    public async Task Index_then_re_index_overwrites_in_place()
    {
        var tenantId = Guid.NewGuid();
        var key = Guid.NewGuid();
        var tenant = new MutableTenant { Id = tenantId };
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using SqliteHarness harness = await SqliteHarness.CreateAsync(tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = key,
            TenantId = tenantId,
            Content = "first revision",
            CharCount = 14,
        }, ct);

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = key,
            TenantId = tenantId,
            Content = "second revision — overwrite",
            CharCount = 27,
        }, ct);

        await using IndexingDbContext db = await harness.Factory.CreateDbContextAsync(ct);
        List<IndexedEntryRow<Guid>> rows = await db.Set<IndexedEntryRow<Guid>>()
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        rows.Count.ShouldBe(1, "re-indexing the same (TenantId, Key) must upsert, not duplicate");
        rows[0].Content.ShouldBe("second revision — overwrite");
        rows[0].CharCount.ShouldBe(27);
    }

    [Fact]
    public async Task Indexes_are_isolated_per_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var sharedKey = Guid.NewGuid();
        MutableTenant tenant = new() { Id = tenantA };
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using SqliteHarness harness = await SqliteHarness.CreateAsync(tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = sharedKey,
            TenantId = tenantA,
            Content = "tenant A document",
            CharCount = 18,
        }, ct);

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = sharedKey,
            TenantId = tenantB,
            Content = "tenant B document",
            CharCount = 18,
        }, ct);

        // Same key, different tenants ⇒ two rows.
        await using IndexingDbContext db = await harness.Factory.CreateDbContextAsync(ct);
        List<IndexedEntryRow<Guid>> rows = await db.Set<IndexedEntryRow<Guid>>()
            .IgnoreQueryFilters()
            .OrderBy(r => r.Content)
            .ToListAsync(ct);
        rows.Count.ShouldBe(2);
        rows[0].Content.ShouldBe("tenant A document");
        rows[1].Content.ShouldBe("tenant B document");
    }

    [Fact]
    public async Task Remove_only_drops_matching_tenant_key_tuple()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var sharedKey = Guid.NewGuid();
        MutableTenant tenant = new() { Id = tenantA };
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using SqliteHarness harness = await SqliteHarness.CreateAsync(tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();

        await indexer.IndexAsync(new IndexedEntry<Guid> { Key = sharedKey, TenantId = tenantA, Content = "A" }, ct);
        await indexer.IndexAsync(new IndexedEntry<Guid> { Key = sharedKey, TenantId = tenantB, Content = "B" }, ct);

        await indexer.RemoveAsync(sharedKey, tenantA, ct);

        await using IndexingDbContext db = await harness.Factory.CreateDbContextAsync(ct);
        List<IndexedEntryRow<Guid>> remaining = await db.Set<IndexedEntryRow<Guid>>()
            .IgnoreQueryFilters()
            .ToListAsync(ct);

        remaining.Count.ShouldBe(1);
        remaining[0].TenantId.ShouldBe(tenantB);
    }
}
