using Granit.Events;
using Granit.Indexing.Diagnostics;
using Granit.Indexing.Events;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore.Internal;

/// <summary>
/// EF-backed <see cref="IIndexer{TKey}"/>. Upserts by <c>(TenantId, Key)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Idempotent: re-indexing the same key for the same tenant overwrites the row in
/// place. The composite primary key (<c>TenantId, Key</c>) makes a cross-tenant write
/// a new row rather than an update — even if the consumer accidentally passes a wrong
/// tenant on update, it cannot overwrite another tenant's row.
/// </para>
/// </remarks>
internal sealed class EfIndexer<TKey> : IIndexer<TKey>
{
    internal const string BackendName = "ef_tsvector";

    private readonly IDbContextFactory<IndexingDbContext> _factory;
    private readonly ICurrentTenant _currentTenant;
    private readonly IndexingMetrics _metrics;
    private readonly ILocalEventBus _eventBus;

    public EfIndexer(
        IDbContextFactory<IndexingDbContext> factory,
        ICurrentTenant currentTenant,
        IndexingMetrics metrics,
        ILocalEventBus eventBus)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(currentTenant);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(eventBus);
        _factory = factory;
        _currentTenant = currentTenant;
        _metrics = metrics;
        _eventBus = eventBus;
    }

    public async Task IndexAsync(IndexedEntry<TKey> entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Guid? tenantId = entry.TenantId ?? _currentTenant.Id;

        await using IndexingDbContext db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        DbSet<IndexedEntryRow<TKey>> set = db.Set<IndexedEntryRow<TKey>>();

        IndexedEntryRow<TKey>? existing = await set
            .IgnoreQueryFilters([Granit.Persistence.EntityFrameworkCore.GranitFilterNames.MultiTenant])
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Key!.Equals(entry.Key), cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            set.Add(new IndexedEntryRow<TKey>
            {
                Key = entry.Key,
                TenantId = tenantId,
                Content = entry.Content,
                Language = entry.Language,
                Summary = entry.Summary,
                Tags = entry.Tags?.ToArray(),
                IsTruncated = entry.IsTruncated,
                CharCount = entry.CharCount,
                DataSubjectId = entry.DataSubjectId,
            });
        }
        else
        {
            existing.Content = entry.Content;
            existing.Language = entry.Language;
            existing.Summary = entry.Summary;
            existing.Tags = entry.Tags?.ToArray();
            existing.IsTruncated = entry.IsTruncated;
            existing.CharCount = entry.CharCount;
            existing.DataSubjectId = entry.DataSubjectId;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _metrics.RecordEntryFailed(tenantId?.ToString(), BackendName, "db_update");
            await _eventBus.PublishAsync(
                new EntryIndexingFailedEvent<TKey>(entry.Key, tenantId, BackendName, "db_update"),
                cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException("Failed to index entry.", ex);
        }

        _metrics.RecordEntryIndexed(tenantId?.ToString(), BackendName);
        await _eventBus.PublishAsync(
            new EntryIndexedEvent<TKey>(entry.Key, tenantId, BackendName),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(TKey key, Guid? tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        await using IndexingDbContext db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Set<IndexedEntryRow<TKey>>()
            .IgnoreQueryFilters([Granit.Persistence.EntityFrameworkCore.GranitFilterNames.MultiTenant])
            .Where(r => r.TenantId == tenantId && r.Key!.Equals(key))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
