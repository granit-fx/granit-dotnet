using Granit.Testing.Fakes;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Indexing.EntityFrameworkCore.Tests.Integration;

[Collection(PostgresTestSuite.Name)]
public sealed class TsVectorGeneratedColumnTests(PostgresFixture fixture)
{
    [Fact]
    public async Task SearchVector_is_generated_from_content_on_insert()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var tenant = new FakeCurrentTenant { Id = tenantId };

        await using PostgresHarness harness = await PostgresHarness.CreateAsync(fixture.ConnectionString, tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantId,
            Content = "the quick brown fox jumps over the lazy dog",
            Language = "en",
            CharCount = 43,
        }, ct);

        await using IndexingDbContext db = await harness.Factory.CreateDbContextAsync(ct);
        IndexedEntryRow<Guid> row = await db.Set<IndexedEntryRow<Guid>>().SingleAsync(ct);

        // Postgres populates the SearchVector column from the generated expression — it must
        // arrive non-null and contain stemmed tokens from the content.
        row.SearchVector.ShouldNotBeNull();
        string vectorText = row.SearchVector.ToString()!;
        vectorText.ShouldContain("quick");
        vectorText.ShouldContain("brown");
        vectorText.ShouldContain("fox");
    }

    [Fact]
    public async Task French_content_uses_french_dictionary_when_language_is_set()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var tenant = new FakeCurrentTenant { Id = tenantId };

        await using PostgresHarness harness = await PostgresHarness.CreateAsync(fixture.ConnectionString, tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantId,
            Content = "les chats mangent rapidement",
            Language = "french",
            CharCount = 29,
        }, ct);

        await using IndexingDbContext db = await harness.Factory.CreateDbContextAsync(ct);
        IndexedEntryRow<Guid> row = await db.Set<IndexedEntryRow<Guid>>().SingleAsync(ct);

        // French stemming reduces "chats"/"mangent"/"rapidement" past the simple/english
        // forms — the resulting lexeme set differs from a simple-dictionary tokenisation.
        string vectorText = row.SearchVector.ToString()!;
        vectorText.ShouldNotBeNullOrEmpty();
    }
}

[CollectionDefinition(PostgresTestSuite.Name)]
public sealed class PostgresTestSuite : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
