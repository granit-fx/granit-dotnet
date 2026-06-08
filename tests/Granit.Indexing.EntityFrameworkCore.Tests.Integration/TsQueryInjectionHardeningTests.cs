using Granit.Testing.Fakes;
using Shouldly;
using Xunit;

namespace Granit.Indexing.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Locks down the tsquery-injection hardening: a query string carrying tsquery operator
/// syntax (<c>&amp;</c>, <c>|</c>, <c>!</c>, parentheses) MUST NOT be interpreted as
/// operators by the default search path. Both <c>plainto_tsquery</c> and
/// <c>websearch_to_tsquery</c> treat the input as natural language. Without this hardening
/// a caller could craft a tsquery that bypasses ranking — or trigger a parser error from
/// Postgres' strict <c>to_tsquery</c>.
/// </summary>
[Collection(PostgresTestSuite.Name)]
public sealed class TsQueryInjectionHardeningTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Operator_chars_in_query_match_the_same_results_as_plain_text()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var tenant = new FakeCurrentTenant { Id = tenantId };

        await using PostgresHarness harness = await PostgresHarness.CreateAsync(fixture.ConnectionString, tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();
        ISearchBackend<Guid, string> backend = harness.Services.GetRequiredService<ISearchBackend<Guid, string>>();

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantId,
            Content = "foo is here",
            Language = "en",
        }, ct);
        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantId,
            Content = "bar is over there",
            Language = "en",
        }, ct);

        // websearch_to_tsquery (default): operator characters become literals; the result
        // set for "foo bar" matches "foo & bar | baz" because neither row contains both
        // "foo" AND "bar".
        BackendSearchPage<Guid, string> baseline = await backend.SearchAsync(
            new SearchRequest("foo bar"), offset: 0, limit: 10, ct);
        BackendSearchPage<Guid, string> tainted = await backend.SearchAsync(
            new SearchRequest("foo & bar | baz"), offset: 0, limit: 10, ct);

        baseline.Hits.Select(h => h.Key).ShouldBe(tainted.Hits.Select(h => h.Key));
    }

    [Fact]
    public async Task Operator_chars_do_not_raise_postgres_syntax_error()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var tenant = new FakeCurrentTenant { Id = tenantId };

        await using PostgresHarness harness = await PostgresHarness.CreateAsync(fixture.ConnectionString, tenant, ct, typeof(Guid));
        IIndexer<Guid> indexer = harness.Services.GetRequiredService<IIndexer<Guid>>();
        ISearchBackend<Guid, string> backend = harness.Services.GetRequiredService<ISearchBackend<Guid, string>>();

        await indexer.IndexAsync(new IndexedEntry<Guid>
        {
            Key = Guid.NewGuid(),
            TenantId = tenantId,
            Content = "anything",
            Language = "en",
        }, ct);

        // Strings that would crash to_tsquery (unbalanced parentheses, raw operators) must
        // be silently accepted by the default search path.
        await Should.NotThrowAsync(async () =>
            await backend.SearchAsync(new SearchRequest("(foo & ! "), offset: 0, limit: 10, ct));
    }
}
