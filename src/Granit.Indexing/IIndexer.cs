namespace Granit.Indexing;

/// <summary>
/// Write-side port for an index backend. One implementation per backend (EF/tsvector,
/// Elasticsearch, vector store) is registered against the host's DI container.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authorization boundary.</b> Implementations MUST persist <see cref="IndexedEntry{TKey}.TenantId"/>
/// and filter on it for every operation. Per-resource ACL is the consumer module's
/// responsibility and is checked at read time via <see cref="ISearchResultAuthorizer{TKey}"/> —
/// never on write.
/// </para>
/// <para>
/// Indexing should be idempotent: re-indexing the same key for the same tenant
/// overwrites the existing row in place.
/// </para>
/// </remarks>
/// <typeparam name="TKey">Strongly-typed primary key of the indexed resource.</typeparam>
public interface IIndexer<TKey>
{
    /// <summary>Inserts or replaces an entry for <c>(TenantId, Key)</c>.</summary>
    /// <param name="entry">Entry to index.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IndexAsync(IndexedEntry<TKey> entry, CancellationToken cancellationToken = default);

    /// <summary>Removes the entry identified by <c>(tenantId, key)</c>, if any.</summary>
    /// <param name="key">Key of the entry to remove.</param>
    /// <param name="tenantId">
    /// Tenant the entry belongs to. <c>null</c> for single-tenant deployments. Passed
    /// explicitly rather than read from ambient state so background workers — which may
    /// not have <see cref="MultiTenancy.ICurrentTenant"/> in scope — can still remove
    /// rows from any tenant without leaking through.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task RemoveAsync(TKey key, Guid? tenantId, CancellationToken cancellationToken = default);
}
