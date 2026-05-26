using Granit.Indexing.EntityFrameworkCore.Options;
using Microsoft.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore.Internal;

/// <summary>
/// EF-backed <see cref="ISearchBackend{TKey, TResult}"/>. Translates the search request
/// into a tsvector match against <see cref="IndexedEntryRow{TKey}.SearchVector"/>,
/// projects to a backend-supplied projection, and orders by Postgres' relevance ranking.
/// </summary>
/// <remarks>
/// <para>
/// <b>tsquery injection hardening.</b> By default the backend builds the tsquery via
/// <c>plainto_tsquery</c> (or <c>websearch_to_tsquery</c> when
/// <see cref="IndexingEntityFrameworkCoreOptions.UseWebSearchSyntax"/> is set) — both
/// functions parse caller input as <b>natural language</b> and silently treat
/// <c>&amp;</c>/<c>|</c>/<c>!</c> as literals rather than tsquery operators.
/// <c>to_tsquery</c> (operator-aware, the historic injection vector) is intentionally
/// not reachable from this path; it is reserved for a future advanced-search backend
/// gated by a <c>Search.Advanced.Execute</c> permission on the consumer's permission tree.
/// </para>
/// <para>
/// <b>Tenant isolation.</b> Inherited from <see cref="IndexingDbContext"/>: the
/// parameterised query filter on <see cref="IndexedEntryRow{TKey}.TenantId"/> is rewritten
/// into the SQL on every command, so cross-tenant queries are impossible from the read
/// path even when the orchestrator over-fetches.
/// </para>
/// </remarks>
internal sealed class EfSearchBackend<TKey, TResult> : ISearchBackend<TKey, TResult>
{
    private readonly IDbContextFactory<IndexingDbContext> _factory;
    private readonly IndexingEntityFrameworkCoreOptions _options;
    private readonly Func<IndexedEntryRow<TKey>, TResult> _projection;

    public EfSearchBackend(
        IDbContextFactory<IndexingDbContext> factory,
        IndexingEntityFrameworkCoreOptions options,
        Func<IndexedEntryRow<TKey>, TResult> projection)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(projection);
        _factory = factory;
        _options = options;
        _projection = projection;
    }

    public string Name => EfIndexer<TKey>.BackendName;

    public async Task<BackendSearchPage<TKey, TResult>> SearchAsync(
        SearchRequest request,
        int offset,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using IndexingDbContext db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        string dictionary = IndexingLanguageMap.GetPostgresDictionary(request.Language, db.DefaultDictionary);
        string queryText = request.Query ?? string.Empty;

        // Fetch one extra row to determine HasMore without a separate COUNT.
        IQueryable<IndexedEntryRow<TKey>> q = db.Set<IndexedEntryRow<TKey>>();

        IQueryable<IndexedEntryRow<TKey>> matched = _options.UseWebSearchSyntax
            ? q.Where(r => r.SearchVector.Matches(EF.Functions.WebSearchToTsQuery(dictionary, queryText)))
                .OrderByDescending(r => r.SearchVector.Rank(EF.Functions.WebSearchToTsQuery(dictionary, queryText)))
                .ThenBy(r => r.Key)
            : q.Where(r => r.SearchVector.Matches(EF.Functions.PlainToTsQuery(dictionary, queryText)))
                .OrderByDescending(r => r.SearchVector.Rank(EF.Functions.PlainToTsQuery(dictionary, queryText)))
                .ThenBy(r => r.Key);

        List<IndexedEntryRow<TKey>> rows = await matched
            .Skip(offset)
            .Take(limit + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        bool hasMore = rows.Count > limit;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        // Rank is recomputed in-memory for the response object; the index path already
        // ranked at the SQL level via the ORDER BY clause. Re-running ts_rank here would
        // require a second projection; for an MVP the response Score is best-effort.
        var hits = new SearchHit<TKey, TResult>[rows.Count];
        for (int i = 0; i < rows.Count; i++)
        {
            hits[i] = new SearchHit<TKey, TResult>(rows[i].Key, _projection(rows[i]), Score: rows.Count - i);
        }

        return new BackendSearchPage<TKey, TResult>(hits, hasMore);
    }

}
