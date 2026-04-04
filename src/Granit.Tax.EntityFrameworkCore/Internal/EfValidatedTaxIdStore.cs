using Granit.Tax.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Tax.EntityFrameworkCore.Internal;

/// <summary>EF Core store for cached tax ID validations.</summary>
internal sealed class EfValidatedTaxIdStore(
    IDbContextFactory<TaxDbContext> contextFactory) : IValidatedTaxIdReader, IValidatedTaxIdWriter
{
    public async Task<ValidatedTaxId?> GetByTaxIdAsync(
        string taxId, CancellationToken cancellationToken = default)
    {
        await using TaxDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.ValidatedTaxIds
            .FirstOrDefaultAsync(v => v.TaxId == taxId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ValidatedTaxId>> GetForTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        await using TaxDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await db.ValidatedTaxIds
            .Where(v => v.TenantId == tenantId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(
        ValidatedTaxId entry, CancellationToken cancellationToken = default)
    {
        await using TaxDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        db.ValidatedTaxIds.Add(entry);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(
        ValidatedTaxId entry, CancellationToken cancellationToken = default)
    {
        await using TaxDbContext db = await contextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        db.ValidatedTaxIds.Update(entry);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
