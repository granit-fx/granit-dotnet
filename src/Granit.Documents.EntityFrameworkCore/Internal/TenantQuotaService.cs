using Granit.Documents.Domain;
using Granit.Documents.Options;
using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed implementation of <see cref="ITenantQuotaService"/>. Lazy creation,
/// atomic write path via <c>ExecuteUpdateAsync</c>.
/// </summary>
internal sealed class TenantQuotaService(
    IDbContextFactory<DocumentsDbContext> contextFactory,
    IGuidGenerator guidGenerator,
    IClock clock,
    IOptions<GranitDocumentsOptions> options) : ITenantQuotaService
{
    /// <inheritdoc />
    public async Task<TenantStorageQuota> EnsureTenantQuotaAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        TenantStorageQuota? existing = await SelectAsync(context, tenantId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing;
        }

        long defaultLimit = options.Value.DefaultTenantQuotaBytes;
        var quota = TenantStorageQuota.Create(guidGenerator.Create(), tenantId, defaultLimit, clock.Now);
        context.TenantStorageQuotas.Add(quota);

        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return quota;
        }
        catch (DbUpdateException)
        {
            // A concurrent caller inserted the row between our SELECT and INSERT — the
            // unique index on (TenantId) rejected our row. Re-issue the SELECT to obtain
            // the winning identifier.
            TenantStorageQuota? winner = await SelectAsync(context, tenantId, cancellationToken).ConfigureAwait(false);
            if (winner is null)
            {
                throw;
            }
            return winner;
        }
    }

    /// <inheritdoc />
    public async Task IncrementAsync(Guid tenantId, long delta, CancellationToken cancellationToken = default)
    {
        if (delta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Increment must be non-negative.");
        }
        if (delta == 0)
        {
            return;
        }

        // Ensure the row exists before issuing the atomic update — without it the UPDATE
        // would silently match zero rows and lose the increment.
        await EnsureTenantQuotaAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = clock.Now;
        await context.TenantStorageQuotas
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(q => q.TenantId == tenantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(q => q.UsageBytes, q => q.UsageBytes + delta)
                .SetProperty(q => q.UpdatedAt, _ => now),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DecrementAsync(Guid tenantId, long delta, CancellationToken cancellationToken = default)
    {
        if (delta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Decrement must be non-negative.");
        }
        if (delta == 0)
        {
            return;
        }

        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Clamp at zero in SQL: GREATEST(usage - delta, 0). EF Core 8 translates
        // System.Math.Max for both Postgres (GREATEST) and SQL Server (CASE WHEN ...).
        DateTimeOffset now = clock.Now;
        await context.TenantStorageQuotas
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(q => q.TenantId == tenantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(q => q.UsageBytes,
                    q => q.UsageBytes - delta < 0 ? 0 : q.UsageBytes - delta)
                .SetProperty(q => q.UpdatedAt, _ => now),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<TenantStorageQuota?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return await SelectAsync(context, tenantId, cancellationToken).ConfigureAwait(false);
    }

    private static Task<TenantStorageQuota?> SelectAsync(
        DocumentsDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        context.TenantStorageQuotas
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(q => q.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);
}
