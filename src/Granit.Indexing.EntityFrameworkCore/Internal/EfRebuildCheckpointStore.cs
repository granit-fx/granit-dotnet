using System.ComponentModel;
using Granit.Indexing.BackgroundJobs;
using Granit.Indexing.BackgroundJobs.Exceptions;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore.Internal;

/// <summary>
/// EF-backed <see cref="IRebuildCheckpointStore{TKey}"/>. Round-trips <typeparamref name="TKey"/>
/// through its <see cref="TypeConverter"/> so the same physical row shape works for any
/// key type (Guid, long, string, …).
/// </summary>
/// <remarks>
/// <para>
/// <b>Multi-tenant filter bypass.</b> Every query call <c>.IgnoreQueryFilters([MultiTenant])</c>
/// because the job's target <c>tenantId</c> is dictated by the message payload and may
/// differ from the ambient <see cref="Granit.MultiTenancy.ICurrentTenant"/> (operator
/// rebuilds use <c>null</c>). Isolation is preserved by the explicit
/// <c>r.TenantId == tenantId</c> predicate on every read/write — DO NOT remove or relax
/// it. The integration test
/// <c>EfRebuildCheckpointStoreCrossTenantIsolationTests</c> locks this invariant.
/// </para>
/// <para>
/// <b>Concurrency.</b> The row implements <see cref="Granit.Domain.IConcurrencyAware"/>,
/// so a duplicate dispatcher racing on the same <c>(TenantId, SourceName)</c> tuple loses
/// on <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> with
/// <see cref="DbUpdateConcurrencyException"/>. This store maps it to a typed
/// <see cref="RebuildAlreadyInProgressException"/> so Wolverine can dead-letter the duplicate
/// instead of dropping silent updates.
/// </para>
/// </remarks>
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

        // Tenant filter bypass — see class remarks. Isolation rests on the explicit
        // equality below; do not remove `r.TenantId == tenantId`.
        IndexingRebuildCheckpointRow? row = await db.Set<IndexingRebuildCheckpointRow>()
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
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

        // Tenant filter bypass — see class remarks.
        IndexingRebuildCheckpointRow? existing = await set
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
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

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new RebuildAlreadyInProgressException(tenantId, sourceName, ex);
        }
    }

    public async Task ClearAsync(
        Guid? tenantId,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);

        await using IndexingDbContext db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Tenant filter bypass — see class remarks.
        await db.Set<IndexingRebuildCheckpointRow>()
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
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
