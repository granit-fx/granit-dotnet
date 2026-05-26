namespace Granit.Indexing;

/// <summary>
/// Wire-shape for an entry handed to <see cref="IIndexer{TKey}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authorization boundary.</b> This record carries NO ACL fields by design. Tenant
/// isolation is the only access-control signal honoured by the framework: every backend
/// MUST filter on <see cref="TenantId"/>. Per-resource ACL (workspace ACL, role-based
/// row-level security) is the consumer module's responsibility and is enforced via
/// <see cref="ISearchResultAuthorizer{TKey}"/> at read time — never serialised into the
/// index. Storing per-resource ACLs in the index would couple cache invalidation to
/// every permission change and create an out-of-band oracle.
/// </para>
/// <para>
/// <see cref="Content"/> is the primary searchable body. Hosts must call
/// <c>Granit.TextExtraction</c> (or equivalent) to produce it from raw uploads; truncation
/// limits are propagated through <see cref="IsTruncated"/> and <see cref="CharCount"/>.
/// </para>
/// </remarks>
/// <typeparam name="TKey">Strongly-typed primary key of the indexed resource.</typeparam>
public sealed record IndexedEntry<TKey>
{
    /// <summary>Primary key of the entity being indexed.</summary>
    public required TKey Key { get; init; }

    /// <summary>
    /// Tenant identifier the entry belongs to. <c>null</c> for single-tenant deployments;
    /// non-null entries are isolated by the backend tenant filter.
    /// </summary>
    public required Guid? TenantId { get; init; }

    /// <summary>Full text to index (extracted body). Required.</summary>
    public required string Content { get; init; }

    /// <summary>
    /// ISO 639-1 language code (e.g. <c>en</c>, <c>fr</c>, <c>zh</c>) detected by an
    /// <see cref="ILanguageDetector"/>. <c>null</c> when detection failed; the backend
    /// falls back to its language-agnostic dictionary (Postgres: <c>simple</c>).
    /// </summary>
    public string? Language { get; init; }

    /// <summary>Optional abstractive summary produced by <see cref="ISummarizer"/>.</summary>
    public string? Summary { get; init; }

    /// <summary>Optional facet tags produced by <see cref="IAutoTagger"/>.</summary>
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>
    /// Optional dense vector embedding. The element type and dimensionality are backend-
    /// and model-specific; <c>Granit.Indexing</c> only ships the contract here (vector
    /// backends and AI providers wire in via I-F2.2 / I-F3.x stories).
    /// </summary>
    public ReadOnlyMemory<float>? Embedding { get; init; }

    /// <summary>
    /// Optional structured facets (<c>key → value</c>) used for filter pivots in the
    /// consumer UI. Avoid PII; persisted as-is by every backend.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Facets { get; init; }

    /// <summary>
    /// <c>true</c> when <see cref="Content"/> was truncated by the producing extractor
    /// (text-extraction cap, OCR confidence floor…). Consumers can surface a hint that
    /// search may miss content past the truncation point.
    /// </summary>
    public bool IsTruncated { get; init; }

    /// <summary>Character count of <see cref="Content"/>, populated by the producer.</summary>
    public int CharCount { get; init; }

    /// <summary>
    /// Optional natural-person identifier the indexed body refers to. Persisted by
    /// backends so the GDPR Art. 17 cascade handler can delete every row tied to a
    /// subject in one statement. <c>null</c> when the resource is non-personal data
    /// (system documents, public reference data).
    /// </summary>
    /// <remarks>
    /// Producers populate this from
    /// <see cref="IIndexedEntrySource{TKey}.GetDataSubjectIdAsync(TKey, CancellationToken)"/>
    /// at build time. Not an ACL field — anyone with tenant access can still see the row
    /// in search results; the subject id only drives erasure.
    /// </remarks>
    public Guid? DataSubjectId { get; init; }
}
