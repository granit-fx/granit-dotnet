namespace Granit.Indexing;

/// <summary>
/// Produces facet tags for an extracted document body. Optional indexing enricher.
/// </summary>
/// <remarks>
/// <para>
/// Implementations accept a consumer-supplied <see cref="ITagCandidateProvider"/> so the
/// auto-tagger respects the tenant's tag universe instead of hallucinating new strings
/// — important for stable facets and predictable cardinality on the index.
/// Concrete implementations ship in I-F3.3 (AI provider).
/// </para>
/// <para>
/// <b>⚠ Suggestion-only UX contract.</b> User-facing UI MUST require explicit
/// confirmation before applying suggested tags. The 'suggestion-only' guarantee is a
/// UX contract — in bulk-approve flows, this defence becomes ineffective.
/// </para>
/// </remarks>
public interface IAutoTagger
{
    /// <summary>
    /// Returns up to <paramref name="maxTags"/> tags for <paramref name="content"/>.
    /// </summary>
    /// <param name="content">Body to tag.</param>
    /// <param name="candidates">Allowed tag universe.</param>
    /// <param name="maxTags">Upper bound on the returned list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> TagAsync(
        string content,
        ITagCandidateProvider candidates,
        int maxTags,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Tag-universe provider implemented by the consumer. Auto-taggers ask for the active
/// tenant's candidate list at tag time so freshly-created tags propagate without an
/// AI-side retrain.
/// </summary>
public interface ITagCandidateProvider
{
    /// <summary>Returns the candidate tags allowed for the current tenant context.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> GetCandidatesAsync(CancellationToken cancellationToken = default);
}
