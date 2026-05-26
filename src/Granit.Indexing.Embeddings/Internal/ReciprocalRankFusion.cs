namespace Granit.Indexing.Embeddings.Internal;

/// <summary>
/// Reciprocal Rank Fusion (Cormack, Clarke, Büttcher 2009). Fuses two ranked lists of
/// <see cref="SearchHit{TKey, TResult}"/> into a single ranking via
/// <c>score(d) = Σ 1 / (k + denseRank_i(d))</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dense ranking on ties.</b> BM25 and Postgres <c>ts_rank</c> regularly emit
/// multiple documents with identical raw scores. Naively enumerating gives the second
/// equal-score doc rank 2 — penalising it for nothing. The fuser walks each list in
/// order and increments the rank counter only when the raw <see cref="SearchHit{TKey,
/// TResult}.Score"/> CHANGES. Scores must be already-sorted descending (every backend
/// in the framework returns them that way).
/// </para>
/// <para>
/// <b>Per-doc tracking.</b> A document appearing in only one list contributes once.
/// Re-occurrences in the same list (shouldn't happen but defensive): only the FIRST
/// rank counts.
/// </para>
/// </remarks>
internal static class ReciprocalRankFusion
{
    /// <summary>Fuses two ranked hit lists into a single descending-score list.</summary>
    /// <typeparam name="TKey">Resource primary key.</typeparam>
    /// <typeparam name="TResult">Projected payload.</typeparam>
    /// <param name="lexical">Lexical (BM25 / tsvector) hits, descending by raw score.</param>
    /// <param name="semantic">Semantic (cosine kNN) hits, descending by raw score.</param>
    /// <param name="k">RRF smoothing constant. Default 60 per Cormack 2009.</param>
    /// <returns>All fused hits ordered by descending RRF score. The returned
    /// <see cref="SearchHit{TKey, TResult}.Score"/> carries the RRF score (not the
    /// original raw score from either channel).</returns>
    public static IReadOnlyList<SearchHit<TKey, TResult>> Fuse<TKey, TResult>(
        IReadOnlyList<SearchHit<TKey, TResult>> lexical,
        IReadOnlyList<SearchHit<TKey, TResult>> semantic,
        int k)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(lexical);
        ArgumentNullException.ThrowIfNull(semantic);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(k);

        Dictionary<TKey, double> scores = [];
        Dictionary<TKey, TResult> payloads = [];

        AccumulateDenseRanks(lexical, k, scores, payloads);
        AccumulateDenseRanks(semantic, k, scores, payloads);

        var fused = new SearchHit<TKey, TResult>[scores.Count];
        int i = 0;
        foreach ((TKey key, double score) in scores.OrderByDescending(static kv => kv.Value))
        {
            fused[i++] = new SearchHit<TKey, TResult>(key, payloads[key], score);
        }
        return fused;
    }

    private static void AccumulateDenseRanks<TKey, TResult>(
        IReadOnlyList<SearchHit<TKey, TResult>> hits,
        int k,
        Dictionary<TKey, double> scores,
        Dictionary<TKey, TResult> payloads)
        where TKey : notnull
    {
        int denseRank = 0;
        double? lastRawScore = null;
        HashSet<TKey> seenInThisList = [];

        foreach (SearchHit<TKey, TResult> hit in hits)
        {
            if (lastRawScore is null || hit.Score != lastRawScore.Value)
            {
                denseRank++;
                lastRawScore = hit.Score;
            }

            // Defensive: re-occurrences in the same list contribute only the first time.
            if (!seenInThisList.Add(hit.Key))
            {
                continue;
            }

            double contribution = 1.0 / (k + denseRank);
            scores[hit.Key] = scores.TryGetValue(hit.Key, out double prior)
                ? prior + contribution
                : contribution;
            payloads.TryAdd(hit.Key, hit.Result);
        }
    }
}
