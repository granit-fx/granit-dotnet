namespace Granit.Indexing;

/// <summary>
/// Consumer-supplied source of entries to feed into <see cref="IIndexer{TKey}"/> — the
/// bridge between domain aggregates and the index pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Implementations cover full rebuilds (<see cref="EnumerateKeysAsync"/>) and on-demand
/// per-entity refresh (<see cref="BuildEntryAsync"/>). The background reindex job
/// (I-F5.1) iterates <see cref="EnumerateKeysAsync"/> and calls
/// <see cref="BuildEntryAsync"/> per key; lifecycle hooks (Wolverine handlers) call
/// <see cref="BuildEntryAsync"/> directly on save.
/// </para>
/// <para>
/// <b>GDPR Art. 17.</b> <see cref="GetDataSubjectIdAsync"/> identifies the natural
/// person whose personal data the indexed body contains, returning <c>null</c> when the
/// resource is non-personal (e.g. system documents). The
/// <c>PersonalDataDeletedEto</c> handler (ships in I-F2.1) reads this value to delete
/// every indexed row tied to the subject — the index would otherwise outlive the source
/// row.
/// </para>
/// </remarks>
public interface IIndexedEntrySource<TKey>
{
    /// <summary>
    /// Stable identifier for the source on metric/log tags (e.g. <c>"document"</c>,
    /// <c>"workspace_note"</c>). Used by the background reindex job to scope its run.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Enumerates every key the source can produce for the given tenant in a stable,
    /// deterministic order (typically ascending primary key). The background reindex
    /// job calls this with the last successfully processed key in
    /// <paramref name="resumeAfter"/> after a crash, expecting the source to start
    /// past that point so no rows are re-indexed.
    /// </summary>
    /// <param name="tenantId">
    /// Tenant scope. <c>null</c> means "all tenants" — used by single-tenant deployments
    /// and ops-driven full rebuilds in multi-tenant hosts that explicitly opt out of
    /// tenant scoping.
    /// </param>
    /// <param name="resumeAfter">
    /// The last key successfully indexed in a prior run. <c>null</c> means "start from
    /// the beginning". The source MUST guarantee that the same enumeration ordering
    /// holds across calls so resume is deterministic.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    IAsyncEnumerable<TKey> EnumerateKeysAsync(
        Guid? tenantId,
        TKey? resumeAfter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the <see cref="IndexedEntry{TKey}"/> for <paramref name="key"/>, or
    /// <c>null</c> if the resource no longer exists.
    /// </summary>
    Task<IndexedEntry<TKey>?> BuildEntryAsync(TKey key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the data-subject identifier the resource refers to, or <c>null</c> when
    /// the resource is not personal data.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Guid?> GetDataSubjectIdAsync(TKey key, CancellationToken cancellationToken = default);
}
