using Granit.Indexing.Embeddings.Internal;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Embeddings.Tests;

public sealed class ReciprocalRankFusionTests
{
    [Fact]
    public void Fuse_returns_descending_score_order()
    {
        // Smoke test on the core invariant: the fused list MUST be sorted descending by
        // the computed RRF score. Without this, the caller's Skip/Take page slice would
        // be meaningless.
        IReadOnlyList<SearchHit<string, string>> lex = Hits([("A", 0.9), ("B", 0.5), ("C", 0.3)]);
        IReadOnlyList<SearchHit<string, string>> sem = Hits([("A", 0.8), ("D", 0.7), ("B", 0.2)]);

        IReadOnlyList<SearchHit<string, string>> fused = ReciprocalRankFusion.Fuse(lex, sem, k: 60);

        for (int i = 1; i < fused.Count; i++)
        {
            fused[i - 1].Score.ShouldBeGreaterThanOrEqualTo(fused[i].Score);
        }
    }

    [Fact]
    public void Fuse_rewards_documents_present_in_both_channels()
    {
        // Doc A appears in both lists at rank 1: it must outscore docs present in only
        // one list. This is the entire point of RRF as a hybrid retrieval primitive.
        IReadOnlyList<SearchHit<string, string>> lex = Hits([("A", 1.0), ("B", 0.5)]);
        IReadOnlyList<SearchHit<string, string>> sem = Hits([("A", 1.0), ("C", 0.5)]);

        IReadOnlyList<SearchHit<string, string>> fused = ReciprocalRankFusion.Fuse(lex, sem, k: 60);

        fused[0].Key.ShouldBe("A");
        fused[0].Score.ShouldBeGreaterThan(fused[1].Score);
    }

    [Fact]
    public void Fuse_with_k_60_matches_published_formula()
    {
        // Lock the RRF math against accidental refactors. A doc at rank 1 in both
        // channels gets 2 × 1/(60 + 1) = 2/61 ≈ 0.03278688524… Anything else means
        // someone broke the contribution formula.
        IReadOnlyList<SearchHit<string, string>> lex = Hits([("A", 1.0)]);
        IReadOnlyList<SearchHit<string, string>> sem = Hits([("A", 1.0)]);

        IReadOnlyList<SearchHit<string, string>> fused = ReciprocalRankFusion.Fuse(lex, sem, k: 60);

        fused[0].Score.ShouldBe(2.0 / 61.0, tolerance: 1e-12);
    }

    [Fact]
    public void Fuse_applies_dense_ranking_so_equal_raw_scores_share_a_rank()
    {
        // The whole point Gemini's review caught: BM25 / ts_rank often emit ties. Naive
        // enumeration would give B rank 2 and C rank 3 even though their raw scores
        // are equal — penalising C unfairly. Dense ranking gives B and C the SAME RRF
        // rank, so their contributions match.
        IReadOnlyList<SearchHit<string, string>> lex = Hits([("A", 1.0), ("B", 0.5), ("C", 0.5), ("D", 0.1)]);
        IReadOnlyList<SearchHit<string, string>> empty = [];

        IReadOnlyList<SearchHit<string, string>> fused = ReciprocalRankFusion.Fuse(lex, empty, k: 60);

        SearchHit<string, string> b = fused.Single(h => h.Key == "B");
        SearchHit<string, string> c = fused.Single(h => h.Key == "C");

        b.Score.ShouldBe(c.Score);
    }

    [Fact]
    public void Fuse_handles_an_empty_channel_by_returning_the_other_channels_ranking()
    {
        IReadOnlyList<SearchHit<string, string>> lex = Hits([("A", 1.0), ("B", 0.5)]);
        IReadOnlyList<SearchHit<string, string>> empty = [];

        IReadOnlyList<SearchHit<string, string>> fused = ReciprocalRankFusion.Fuse(lex, empty, k: 60);

        fused.Count.ShouldBe(2);
        fused[0].Key.ShouldBe("A");
        fused[1].Key.ShouldBe("B");
    }

    [Fact]
    public void Fuse_deduplicates_documents_appearing_in_a_single_list()
    {
        // Defensive: if a backend emits a doc twice (shouldn't but real-world bugs
        // happen), the fuser counts only the first occurrence to keep the score sane.
        IReadOnlyList<SearchHit<string, string>> lex = Hits([("A", 1.0), ("A", 0.9)]);
        IReadOnlyList<SearchHit<string, string>> empty = [];

        IReadOnlyList<SearchHit<string, string>> fused = ReciprocalRankFusion.Fuse(lex, empty, k: 60);

        fused.Count.ShouldBe(1);
    }

    [Fact]
    public void Fuse_rejects_non_positive_k()
    {
        IReadOnlyList<SearchHit<string, string>> empty = [];
        Should.Throw<ArgumentOutOfRangeException>(() => ReciprocalRankFusion.Fuse(empty, empty, k: 0));
        Should.Throw<ArgumentOutOfRangeException>(() => ReciprocalRankFusion.Fuse(empty, empty, k: -1));
    }

    private static IReadOnlyList<SearchHit<string, string>> Hits(params (string Key, double Score)[] items) =>
        [.. items.Select(t => new SearchHit<string, string>(t.Key, t.Key, t.Score))];
}
