namespace Granit.Indexing.Elasticsearch;

/// <summary>
/// Document shape persisted in Elasticsearch for a single indexed entry.
/// </summary>
/// <remarks>
/// <para>
/// Public so consumer projection delegates can read every field (key, content, tags…)
/// without paying the cost of a second deserialisation. The document carries the
/// <c>tenant_id</c> + <c>data_subject_id</c> fields the framework needs for isolation and
/// GDPR cascades — never permission state (per the framework's authorization boundary).
/// </para>
/// <para>
/// <c>Key</c> is stored as a string: ES does not have a notion of strongly-typed keys, so
/// the indexer round-trips through <c>TKey.ToString()</c>. Consumers project back to
/// their <c>TKey</c> via the projection delegate registered with
/// <c>AddGranitIndexingElasticsearchBackend</c>.
/// </para>
/// </remarks>
public sealed class IndexedEntryDocument
{
    /// <summary>Resource primary key, serialised via <see cref="object.ToString"/>.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Owning tenant. <c>null</c> for single-tenant deployments.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Full text body. Persisted only when <c>StoreFullContentInIndex</c> is <c>true</c>.</summary>
    public string? Content { get; set; }

    /// <summary>ISO 639-1 language code used to pick the analyzer.</summary>
    public string? Language { get; set; }

    /// <summary>Optional abstractive summary.</summary>
    public string? Summary { get; set; }

    /// <summary>Optional facet tags.</summary>
    public string[]? Tags { get; set; }

    /// <summary>True when the source text was truncated by the producing extractor.</summary>
    public bool IsTruncated { get; set; }

    /// <summary>Character count of <see cref="Content"/> as produced.</summary>
    public int CharCount { get; set; }

    /// <summary>
    /// Optional data-subject identifier. Persisted so the GDPR Art. 17 eraser can run a
    /// single <c>delete_by_query</c> targeting <c>tenant_id + data_subject_id</c>.
    /// </summary>
    public Guid? DataSubjectId { get; set; }

    /// <summary>
    /// Optional semantic embedding. Persisted in the SAME document as <see cref="Content"/>
    /// so the existing <c>delete_by_query</c> GDPR cascade purges both atoms atomically
    /// (VULN-201). Mapped as <c>dense_vector</c> with the dimensionality configured in
    /// <c>Granit.Indexing.Embeddings.Options.GranitIndexingEmbeddingsOptions.Dimensions</c>
    /// — when that option is unset, the field is dropped from the index mapping and
    /// always serialises as <c>null</c>.
    /// </summary>
    public float[]? Embedding { get; set; }
}
