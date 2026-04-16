using Granit.Payments.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Payments.EntityFrameworkCore.Internal;

/// <summary>EF Core store for host-level payment method configurations.</summary>
internal sealed class EfPaymentMethodConfigurationStore(
    IDbContextFactory<PaymentsDbContext> contextFactory)
    : IPaymentMethodConfigurationReader, IPaymentMethodConfigurationWriter
{
    // ── Reader (AsNoTracking for all reads) ─────────────────────────────

    public async Task<IReadOnlyList<PaymentMethodConfiguration>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.PaymentMethodConfigurations
            .AsNoTracking()
            .OrderBy(c => c.ProviderName).ThenBy(c => c.MethodType)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PaymentMethodConfiguration>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.PaymentMethodConfigurations
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.ProviderName).ThenBy(c => c.MethodType)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PaymentMethodConfiguration?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        await using PaymentsDbContext db = await contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.PaymentMethodConfigurations
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
}
