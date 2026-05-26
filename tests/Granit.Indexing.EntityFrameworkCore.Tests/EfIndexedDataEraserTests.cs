using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Indexing.EntityFrameworkCore.Tests;

public sealed class EfIndexedDataEraserTests
{
    [Fact]
    public async Task Erases_every_row_for_the_subject_in_the_tenant()
    {
        var tenantId = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var otherSubject = Guid.NewGuid();
        MutableTenant tenant = new() { Id = tenantId };
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using SqliteHarness harness = await SqliteHarness.CreateAsync(tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();
        IIndexedDataEraser eraser = harness.Services.GetRequiredService<IIndexedDataEraser>();

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantId,
            Content = "doc 1",
            DataSubjectId = subject,
        }, ct);
        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantId,
            Content = "doc 2",
            DataSubjectId = subject,
        }, ct);
        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantId,
            Content = "doc 3",
            DataSubjectId = otherSubject,
        }, ct);

        int erased = await eraser.EraseAsync(tenantId, subject, ct);

        erased.ShouldBe(2);

        await using IndexingDbContext db = await harness.Factory.CreateDbContextAsync(ct);
        List<IndexedEntryRow<Guid>> remaining = await db.Set<IndexedEntryRow<Guid>>()
            .IgnoreQueryFilters()
            .ToListAsync(ct);
        remaining.Count.ShouldBe(1);
        remaining[0].DataSubjectId.ShouldBe(otherSubject);
    }

    [Fact]
    public async Task Other_tenants_are_untouched()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var sharedSubject = Guid.NewGuid();
        MutableTenant tenant = new() { Id = tenantA };
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using SqliteHarness harness = await SqliteHarness.CreateAsync(tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();
        IIndexedDataEraser eraser = harness.Services.GetRequiredService<IIndexedDataEraser>();

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantA,
            Content = "A",
            DataSubjectId = sharedSubject,
        }, ct);
        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantB,
            Content = "B",
            DataSubjectId = sharedSubject,
        }, ct);

        int erased = await eraser.EraseAsync(tenantA, sharedSubject, ct);
        erased.ShouldBe(1);

        await using IndexingDbContext db = await harness.Factory.CreateDbContextAsync(ct);
        List<IndexedEntryRow<Guid>> remaining = await db.Set<IndexedEntryRow<Guid>>()
            .IgnoreQueryFilters()
            .ToListAsync(ct);
        remaining.Count.ShouldBe(1);
        remaining[0].TenantId.ShouldBe(tenantB);
    }
}
