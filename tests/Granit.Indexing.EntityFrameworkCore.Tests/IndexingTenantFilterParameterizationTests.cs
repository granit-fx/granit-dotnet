// =============================================================================
// Regression — IndexingDbContext tenant filter must remain parameterised
// =============================================================================
// Locks the GranitDbContext-derived multi-tenant filter for IndexingDbContext.
// If a future refactor reintroduces a closure-captured tenant id (the original
// bug — see MultiTenantFilterParameterizationReproTests in
// Granit.Persistence.EntityFrameworkCore.Tests for the full incident note),
// requests from tenant B will read rows pinned to tenant A's identifier on
// the cached model.
// =============================================================================

using Granit.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Indexing.EntityFrameworkCore.Tests;

public sealed class IndexingTenantFilterParameterizationTests
{
    [Fact]
    public async Task Tenant_filter_re_evaluates_across_dbcontext_instances_of_same_type()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        FakeCurrentTenant tenant = new() { Id = tenantA };
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using SqliteHarness harness = await SqliteHarness.CreateAsync(tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();

        await indexer.IndexAsync(new IndexedEntry<Guid> { Key = Guid.NewGuid(), TenantId = tenantA, Content = "a" }, ct);
        await indexer.IndexAsync(new IndexedEntry<Guid> { Key = Guid.NewGuid(), TenantId = tenantB, Content = "b" }, ct);

        await using (IndexingDbContext ctx1 = await harness.Factory.CreateDbContextAsync(ct))
        {
            tenant.Id = tenantA;
            List<IndexedEntryRow<Guid>> a = await ctx1.Set<IndexedEntryRow<Guid>>().ToListAsync(ct);
            a.Count.ShouldBe(1);
            a[0].Content.ShouldBe("a");
        }

        await using (IndexingDbContext ctx2 = await harness.Factory.CreateDbContextAsync(ct))
        {
            tenant.Id = tenantB;
            List<IndexedEntryRow<Guid>> b = await ctx2.Set<IndexedEntryRow<Guid>>().ToListAsync(ct);
            b.Count.ShouldBe(1, "tenant B should see its own row, NOT tenant A's");
            b[0].Content.ShouldBe("b");
        }
    }

    [Fact]
    public async Task Filter_returns_only_active_tenant_rows_when_other_tenants_share_table()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var tenantC = Guid.NewGuid();
        var tenant = new FakeCurrentTenant { Id = tenantA };
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using SqliteHarness harness = await SqliteHarness.CreateAsync(tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();

        // Seed 3 rows across 3 tenants. Any tenant should only ever see their own.
        await indexer.IndexAsync(new IndexedEntry<Guid> { Key = Guid.NewGuid(), TenantId = tenantA, Content = "a" }, ct);
        await indexer.IndexAsync(new IndexedEntry<Guid> { Key = Guid.NewGuid(), TenantId = tenantB, Content = "b" }, ct);
        await indexer.IndexAsync(new IndexedEntry<Guid> { Key = Guid.NewGuid(), TenantId = tenantC, Content = "c" }, ct);

        await using (IndexingDbContext ctx = await harness.Factory.CreateDbContextAsync(ct))
        {
            tenant.Id = tenantA;
            (await ctx.Set<IndexedEntryRow<Guid>>().CountAsync(ct)).ShouldBe(1);
        }

        await using (IndexingDbContext ctx = await harness.Factory.CreateDbContextAsync(ct))
        {
            tenant.Id = tenantB;
            (await ctx.Set<IndexedEntryRow<Guid>>().CountAsync(ct)).ShouldBe(1);
        }
    }
}
