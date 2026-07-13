using Granit.Persistence.EntityFrameworkCore;
using Granit.Privacy.DataExport;
using Granit.Privacy.EntityFrameworkCore.Entities;
using Granit.Privacy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore.DataExport.Internal;

/// <summary>
/// EF-backed <see cref="IExportAssemblyCheckpointStore"/>. Persists one row per
/// <c>(RequestId, TenantId)</c> tuple so a crashed assembly worker can resume past
/// the last committed shard on re-dispatch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Multi-tenant filter bypass.</b> Every query calls
/// <c>.IgnoreQueryFilters([MultiTenant])</c> because the job's target
/// <c>tenantId</c> is dictated by the message payload, which may differ from the
/// ambient <see cref="Granit.MultiTenancy.ICurrentTenant"/>. Isolation rests on the
/// explicit <c>r.TenantId == tenantId</c> equality predicate on every read/write —
/// DO NOT remove or relax it. The integration test
/// <c>EfExportAssemblyCheckpointStoreCrossTenantIsolationTests</c> locks this
/// invariant.
/// </para>
/// <para>
/// <b>Concurrency.</b> The row implements <see cref="Granit.Domain.IConcurrencyAware"/>;
/// a duplicate dispatcher racing on the same tuple loses on
/// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> — with
/// <see cref="DbUpdateConcurrencyException"/> on the update path, or with a plain
/// <see cref="DbUpdateException"/> (primary-key violation) when both racers observed no
/// existing row and try to insert. This store currently rethrows the EF exception —
/// Wolverine's retry-with-cooldown surface wraps it for DLQ (lands in P6.3c.5).
/// </para>
/// </remarks>
internal sealed class EfExportAssemblyCheckpointStore(
    IDbContextFactory<PrivacyDbContext> factory,
    TimeProvider timeProvider) : IExportAssemblyCheckpointStore
{
    public async Task<ExportAssemblyCheckpoint?> GetAsync(
        Guid requestId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        await using PrivacyDbContext db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Tenant filter bypass — see class remarks. Isolation rests on the explicit
        // equality below; do not remove `r.TenantId == tenantId`.
        ExportAssemblyCheckpointRow? row = await db.Set<ExportAssemblyCheckpointRow>()
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == requestId && r.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        return new ExportAssemblyCheckpoint(
            LastCompletedShardIndex: row.LastCompletedShardIndex,
            NextFragmentIndex: row.NextFragmentIndex,
            CompletedShardObjectKeys: row.CompletedShardObjectKeys.AsReadOnly());
    }

    public async Task SetAsync(
        Guid requestId,
        Guid? tenantId,
        ExportAssemblyCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        await using PrivacyDbContext db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        DbSet<ExportAssemblyCheckpointRow> set = db.Set<ExportAssemblyCheckpointRow>();

        // Tenant filter bypass — see class remarks.
        ExportAssemblyCheckpointRow? existing = await set
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .FirstOrDefaultAsync(r => r.RequestId == requestId && r.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = timeProvider.GetUtcNow();

        if (existing is null)
        {
            set.Add(new ExportAssemblyCheckpointRow
            {
                RequestId = requestId,
                TenantId = tenantId,
                LastCompletedShardIndex = checkpoint.LastCompletedShardIndex,
                NextFragmentIndex = checkpoint.NextFragmentIndex,
                CompletedShardObjectKeys = [.. checkpoint.CompletedShardObjectKeys],
                UpdatedAt = now,
            });
        }
        else
        {
            existing.LastCompletedShardIndex = checkpoint.LastCompletedShardIndex;
            existing.NextFragmentIndex = checkpoint.NextFragmentIndex;
            existing.CompletedShardObjectKeys = [.. checkpoint.CompletedShardObjectKeys];
            existing.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(
        Guid requestId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        await using PrivacyDbContext db = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Tenant filter bypass — see class remarks. The `r.TenantId == tenantId`
        // predicate is the sole guard against `ExecuteDelete` wiping rows belonging
        // to other tenants.
        await db.Set<ExportAssemblyCheckpointRow>()
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(r => r.RequestId == requestId && r.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
