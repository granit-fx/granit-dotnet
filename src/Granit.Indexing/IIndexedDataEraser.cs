namespace Granit.Indexing;

/// <summary>
/// Backend-supplied GDPR Art. 17 hook: erases every indexed row tied to a data subject
/// within the given tenant. One implementation per backend (EF/tsvector,
/// Elasticsearch, vector store) is registered as <see cref="IIndexedDataEraser"/> in DI.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a separate hook instead of <see cref="IIndexer{TKey}.RemoveAsync"/>.</b>
/// <see cref="IIndexer{TKey}.RemoveAsync"/> removes a single known
/// <c>(TenantId, Key)</c> tuple. The GDPR cascade does not know the keys — only the
/// <see cref="IndexedEntry{TKey}.DataSubjectId"/> the entries reference. Backends store
/// that value at index time and expose this method to erase by subject in one bulk
/// statement (<c>ExecuteDelete()</c> on Postgres, <c>delete_by_query</c> on
/// Elasticsearch). Calling <see cref="IIndexer{TKey}.RemoveAsync"/> in a loop would
/// require enumerating every key first — much slower and racier.
/// </para>
/// <para>
/// <b>Idempotent.</b> Repeated calls converge to the same state — Wolverine handler
/// retries and manual replays MUST be safe.
/// </para>
/// </remarks>
public interface IIndexedDataEraser
{
    /// <summary>Backend identifier emitted on metric tags (matches the indexer name).</summary>
    string Name { get; }

    /// <summary>
    /// Erases every indexed row in <paramref name="tenantId"/> whose
    /// <see cref="IndexedEntry{TKey}.DataSubjectId"/> equals
    /// <paramref name="dataSubjectId"/>. Returns the number of rows removed (for audit).
    /// </summary>
    Task<int> EraseAsync(Guid? tenantId, Guid dataSubjectId, CancellationToken cancellationToken = default);
}
