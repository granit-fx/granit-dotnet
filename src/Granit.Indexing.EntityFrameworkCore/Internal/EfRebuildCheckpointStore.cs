using System.ComponentModel;
using Granit.Indexing.BackgroundJobs;
using Microsoft.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore.Internal;

/// <summary>
/// EF-backed <see cref="IRebuildCheckpointStore{TKey}"/>. Round-trips <typeparamref name="TKey"/>
/// through its <see cref="TypeConverter"/> so the same physical row shape works for any
/// key type (Guid, long, string, …).
/// </summary>
internal sealed class EfRebuildCheckpointStore<TKey> : IRebuildCheckpointStore<TKey>
    where TKey : notnull
{
    private static readonly TypeConverter KeyConverter = TypeDescriptor.GetConverter(typeof(TKey));

    private readonly IDbContextFactory<IndexingDbContext> _factory;
    private readonly TimeProvider _timeProvider;

    public EfRebuildCheckpointStore(
        IDbContextFactory<IndexingDbContext> factory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _factory = factory;
        _timeProvider = timeProvider;
    }

    public async Task<TKey?> GetLastCheckpointAsync(
        Guid? tenantId,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        await using IndexingDbContext db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        IndexingRebuildCheckpointRow? row = await db.Set<IndexingRebuildCheckpointRow>()
            .IgnoreQueryFilters([Granit.Persistence.EntityFrameworkCore.GranitFilterNames.MultiTenant])
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.SourceName == sourceName, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return default;
        }

        return Parse(row.LastProcessedKey);
    }

    public async Task SetCheckpointAsync(
        Guid? tenantId,
        string sourceName,
        TKey checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentNullException.ThrowIfNull(checkpoint);

        string keyText = KeyConverter.ConvertToInvariantString(checkpoint)
            ?? throw new InvalidOperationException(
                $"TypeConverter for {typeof(TKey).Name} returned null — checkpoint cannot be persisted. "
                + "Implement a TypeConverter that round-trips to/from string for your custom key type.");

        await using IndexingDbContext db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        DbSet<IndexingRebuildCheckpointRow> set = db.Set<IndexingRebuildCheckpointRow>();

        IndexingRebuildCheckpointRow? existing = await set
            .IgnoreQueryFilters([Granit.Persistence.EntityFrameworkCore.GranitFilterNames.MultiTenant])
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.SourceName == sourceName, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        if (existing is null)
        {
            set.Add(new IndexingRebuildCheckpointRow
            {
                TenantId = tenantId,
                SourceName = sourceName,
                LastProcessedKey = keyText,
                UpdatedAt = now,
            });
        }
        else
        {
            existing.LastProcessedKey = keyText;
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(
        Guid? tenantId,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        await using IndexingDbContext db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Set<IndexingRebuildCheckpointRow>()
            .IgnoreQueryFilters([Granit.Persistence.EntityFrameworkCore.GranitFilterNames.MultiTenant])
            .Where(r => r.TenantId == tenantId && r.SourceName == sourceName)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static TKey? Parse(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return default;
        }

        object? converted = KeyConverter.ConvertFromInvariantString(raw);
        return converted is TKey typed ? typed : default;
    }
}
