using Granit.DataFiltering;
using Granit.Domain;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Payments.EntityFrameworkCore.Internal;

/// <summary>EF Core store for host-level payment method configurations.</summary>
/// <remarks>
/// <para>
/// <see cref="PaymentMethodConfiguration"/> implements <see cref="IActive"/>, so the
/// EF Core global query filter hides deactivated rows by default. The reads that
/// must see both active and inactive records (admin listing, race-safe upsert,
/// resync) bypass the filter per-query via
/// <c>queryable.IgnoreQueryFilters([GranitFilterNames.Active])</c> instead of
/// <see cref="IDataFilter.Disable{TFilter}"/>. The hot-path
/// <see cref="GetActiveAsync"/> relies on the filter and carries no manual
/// <c>.Where(c => c.Activated)</c>.
/// </para>
/// <para>
/// The per-query bypass is the documented recommended pattern (see
/// <c>ApplyGranitConventions</c>). It avoids the static <c>AsyncLocal</c> state
/// surface area inside <c>DataFilter</c> — important under parallel test execution
/// where AsyncLocal flows from sibling test classes can otherwise interleave (cf.
/// <c>project_current_tenant_asynclocal_leak</c>). The injected
/// <see cref="IDataFilter"/> is kept on the constructor for future scenarios that
/// genuinely need flow-scoped bypass; today it is unused.
/// </para>
/// </remarks>
internal sealed class EfPaymentMethodConfigurationStore(
    IDbContextFactory<PaymentsDbContext> contextFactory,
    IDataFilter dataFilter)
    : IPaymentMethodConfigurationReader, IPaymentMethodConfigurationWriter
{
    private static readonly string[] BypassActive = [GranitFilterNames.Active];

    // Reserved for future scenarios that genuinely need flow-scoped bypass.
    // Today, all bypasses are per-query via IgnoreQueryFilters above.
    private readonly IDataFilter _dataFilter = dataFilter;

    // ── Reader (AsNoTracking for all reads) ─────────────────────────────

    public async Task<IReadOnlyList<PaymentMethodConfiguration>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.PaymentMethodConfigurations
            .IgnoreQueryFilters(BypassActive)
            .AsNoTracking()
            .OrderBy(c => c.ProviderName).ThenBy(c => c.MethodType)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PaymentMethodConfiguration>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // IActive query filter handles the Activated = true predicate.
        return await db.PaymentMethodConfigurations
            .AsNoTracking()
            .OrderBy(c => c.ProviderName).ThenBy(c => c.MethodType)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PaymentMethodConfiguration?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.PaymentMethodConfigurations
            .IgnoreQueryFilters(BypassActive)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PaymentMethodConfiguration?> FindAsync(
        string providerName, string methodType, CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.PaymentMethodConfigurations
            .IgnoreQueryFilters(BypassActive)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.ProviderName == providerName && c.MethodType == methodType,
                cancellationToken)
            .ConfigureAwait(false);
    }

    // ── Writer ──────────────────────────────────────────────────────────

    public async Task AddAsync(
        PaymentMethodConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        db.PaymentMethodConfigurations.Add(configuration);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        PaymentMethodConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        db.PaymentMethodConfigurations.Update(configuration);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(
        PaymentMethodConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        db.PaymentMethodConfigurations.Remove(configuration);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertActivationAsync(
        Guid newId,
        string providerName,
        string methodType,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        PaymentMethodConfiguration? existing = await db.PaymentMethodConfigurations
            .IgnoreQueryFilters(BypassActive)
            .FirstOrDefaultAsync(
                c => c.ProviderName == providerName && c.MethodType == methodType,
                cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            var created = PaymentMethodConfiguration
                .Activate(newId, providerName, methodType);

            if (!isActive)
            {
                created.Deactivate();
            }

            db.PaymentMethodConfigurations.Add(created);

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateException)
            {
                // Concurrent POST won the race — fall through to read + update.
                db.PaymentMethodConfigurations.Remove(created);
                db.ChangeTracker.Clear();

                existing = await db.PaymentMethodConfigurations
                    .IgnoreQueryFilters(BypassActive)
                    .FirstOrDefaultAsync(
                        c => c.ProviderName == providerName && c.MethodType == methodType,
                        cancellationToken).ConfigureAwait(false);

                if (existing is null)
                {
                    throw;
                }
            }
        }

        if (isActive)
        {
            existing.Activate();
        }
        else
        {
            existing.Deactivate();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertActivationWithSnapshotAsync(
        Guid newId,
        string providerName,
        string methodType,
        PaymentMethodCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capability);

        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        PaymentMethodConfiguration? existing = await db.PaymentMethodConfigurations
            .IgnoreQueryFilters(BypassActive)
            .FirstOrDefaultAsync(
                c => c.ProviderName == providerName && c.MethodType == methodType,
                cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            var created = PaymentMethodConfiguration.Activate(newId, providerName, methodType);
            created.SnapshotCapability(capability);

            db.PaymentMethodConfigurations.Add(created);

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateException)
            {
                db.PaymentMethodConfigurations.Remove(created);
                db.ChangeTracker.Clear();

                existing = await db.PaymentMethodConfigurations
                    .IgnoreQueryFilters(BypassActive)
                    .FirstOrDefaultAsync(
                        c => c.ProviderName == providerName && c.MethodType == methodType,
                        cancellationToken).ConfigureAwait(false);

                if (existing is null)
                {
                    throw;
                }
            }
        }

        existing.Activate();
        existing.SnapshotCapability(capability);

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> UpdateCapabilitySnapshotAsync(
        string providerName,
        string methodType,
        PaymentMethodCapability capability,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capability);

        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        PaymentMethodConfiguration? existing = await db.PaymentMethodConfigurations
            .IgnoreQueryFilters(BypassActive)
            .FirstOrDefaultAsync(
                c => c.ProviderName == providerName && c.MethodType == methodType,
                cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            return false;
        }

        existing.SnapshotCapability(capability);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
