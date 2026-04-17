using Granit.DataFiltering;
using Granit.Domain;
using Granit.Payments.Contracts;
using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Payments.EntityFrameworkCore.Internal;

/// <summary>EF Core store for host-level payment method configurations.</summary>
/// <remarks>
/// <para>
/// <see cref="PaymentMethodConfiguration"/> implements <see cref="IActive"/>, so the
/// EF Core global query filter hides deactivated rows by default. The <em>reads</em>
/// that must see both active and inactive records (admin listing, race-safe upsert,
/// resync) bypass the filter with
/// <see cref="IDataFilter.Disable{TFilterMarker}"/>. The hot-path
/// <see cref="GetActiveAsync"/> relies on the filter and carries no manual
/// <c>.Where(c => c.Activated)</c>.
/// </para>
/// </remarks>
internal sealed class EfPaymentMethodConfigurationStore(
    IDbContextFactory<PaymentsDbContext> contextFactory,
    IDataFilter dataFilter)
    : IPaymentMethodConfigurationReader, IPaymentMethodConfigurationWriter
{
    // ── Reader (AsNoTracking for all reads) ─────────────────────────────

    public async Task<IReadOnlyList<PaymentMethodConfiguration>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        using (dataFilter.Disable<IActive>())
        {
            return await db.PaymentMethodConfigurations
                .AsNoTracking()
                .OrderBy(c => c.ProviderName).ThenBy(c => c.MethodType)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }
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

        using (dataFilter.Disable<IActive>())
        {
            return await db.PaymentMethodConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<PaymentMethodConfiguration?> FindAsync(
        string providerName, string methodType, CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        using (dataFilter.Disable<IActive>())
        {
            return await db.PaymentMethodConfigurations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.ProviderName == providerName && c.MethodType == methodType,
                    cancellationToken)
                .ConfigureAwait(false);
        }
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

        using (dataFilter.Disable<IActive>())
        {
            PaymentMethodConfiguration? existing = await db.PaymentMethodConfigurations
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

        using (dataFilter.Disable<IActive>())
        {
            PaymentMethodConfiguration? existing = await db.PaymentMethodConfigurations
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

        using (dataFilter.Disable<IActive>())
        {
            PaymentMethodConfiguration? existing = await db.PaymentMethodConfigurations
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
}
