using Granit.Indexing.Privacy;
using Granit.MultiTenancy;
using Granit.Privacy.DataDeletion.Events;
using Granit.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Indexing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// End-to-end privacy-cascade test: a personal-data deletion request reaches the
/// indexing privacy bridge, the EF eraser runs a single <c>ExecuteDelete</c> per
/// registered TKey, and only rows tied to the subject in the calling tenant are removed.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed class PersonalDataDeletionCascadeTests(PostgresFixture fixture)
{
    [Fact]
    public async Task IndexedEntryRow_is_removed_on_PersonalDataDeletionRequestedEto()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var subject = Guid.NewGuid();
        var otherSubject = Guid.NewGuid();
        var tenant = new FakeCurrentTenant { Id = tenantA };

        await using PostgresHarness harness = await PostgresHarness.CreateAsync(fixture.ConnectionString, tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();
        IIndexedDataEraser eraser = harness.Services.GetRequiredService<IIndexedDataEraser>();

        // Three rows: one for the subject in tenant A, one for another subject in tenant A,
        // one for the same subject but a different tenant. After the cascade only the
        // first row must vanish.
        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantA,
            DataSubjectId = subject,
            Content = "personal A",
        }, ct);
        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantA,
            DataSubjectId = otherSubject,
            Content = "other",
        }, ct);
        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantB,
            DataSubjectId = subject,
            Content = "personal B",
        }, ct);

        PersonalDataDeletionRequestedEto @event = new(
            RequestId: Guid.NewGuid(),
            UserId: subject,
            RequestedBy: "data-subject@example.com",
            RequestedAt: DateTimeOffset.UtcNow,
            Reason: "GDPR Art. 17",
            Regulation: "GDPR",
            TenantId: tenantA);

        await PersonalDataDeletionHandler.Handle(
            @event,
            [eraser],
            NullTenantContext.Instance,
            ct);

        await using IndexingDbContext db = await harness.Factory.CreateDbContextAsync(ct);
        List<IndexedEntryRow<Guid>> remaining = await db.Set<IndexedEntryRow<Guid>>()
            .IgnoreQueryFilters()
            .OrderBy(r => r.Content)
            .ToListAsync(ct);

        remaining.Count.ShouldBe(2);
        remaining.ShouldNotContain(r => r.TenantId == tenantA && r.DataSubjectId == subject);
        remaining.ShouldContain(r => r.TenantId == tenantA && r.DataSubjectId == otherSubject);
        remaining.ShouldContain(r => r.TenantId == tenantB && r.DataSubjectId == subject);
    }
}
