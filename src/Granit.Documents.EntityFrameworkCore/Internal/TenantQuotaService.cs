using Granit.Documents;
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
    public async Task<bool> TryReserveAsync(Guid tenantId, long delta, CancellationToken cancellationToken = default)
    {
        if (delta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Reservation must be non-negative.");
        }
        if (delta == 0)
        {
            return true;
        }

        // Lazy-create so the conditional UPDATE has a row to match. The bootstrap also
        // seeds LimitBytes from options.
        await EnsureTenantQuotaAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = clock.Now;
        // Conditional atomic update — the WHERE clause guards against quota overshoot
        // when several concurrent finalisations from the same tenant race. Returns the
        // affected row count: 0 means the predicate failed (would-exceed); 1 means we
        // reserved the bytes.
        int affected = await context.TenantStorageQuotas
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(q => q.TenantId == tenantId && q.UsageBytes + delta <= q.LimitBytes)
            .ExecuteUpdateAsync(s => s
                .SetProperty(q => q.UsageBytes, q => q.UsageBytes + delta)
                .SetProperty(q => q.UpdatedAt, _ => now),
                cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }

    /// <inheritdoc />
    public async Task IncrementRenditionAsync(Guid tenantId, long delta, CancellationToken cancellationToken = default)
    {
        if (delta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta), "Increment must be non-negative.");
        }
        if (delta == 0)
        {
            return;
        }

        // Lazy-create the row so the atomic UPDATE has a target. Mirrors the user-side
        // IncrementAsync path; rendition increments never reject (renditions aren't gated).
        await EnsureTenantQuotaAsync(tenantId, cancellationToken).ConfigureAwait(false);

        await using DocumentsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset now = clock.Now;
        await context.TenantStorageQuotas
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(q => q.TenantId == tenantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(q => q.RenditionUsageBytes, q => q.RenditionUsageBytes + delta)
                .SetProperty(q => q.UpdatedAt, _ => now),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DecrementRenditionAsync(Guid tenantId, long delta, CancellationToken cancellationToken = default)
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

        DateTimeOffset now = clock.Now;
        await context.TenantStorageQuotas
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(q => q.TenantId == tenantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(q => q.RenditionUsageBytes,
                    q => q.RenditionUsageBytes - delta < 0 ? 0 : q.RenditionUsageBytes - delta)
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
