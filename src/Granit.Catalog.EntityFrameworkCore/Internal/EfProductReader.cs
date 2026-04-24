using Granit.Catalog.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Catalog.EntityFrameworkCore.Internal;

internal sealed class EfProductReader(
    IDbContextFactory<CatalogDbContext> contextFactory)
    : EfStoreBase<Product, CatalogDbContext>(contextFactory),
      IProductReader
{
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ReadAsync(
            async db => await Query(db)
                .Include(p => p.ExternalMappings)
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);

    public Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        return ReadAsync(
            async db => await Query(db)
                .Include(p => p.ExternalMappings)
                .FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }

    public Task<IReadOnlyList<Product>> GetByStatusAsync(
        WorkflowLifecycleStatus status,
        CancellationToken cancellationToken = default) =>
        ReadAsync(
            async db =>
            {
                List<Product> products = await Query(db)
                    .Include(p => p.ExternalMappings)
                    .Where(p => p.LifecycleStatus == status)
                    .OrderBy(p => p.Sku)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                return (IReadOnlyList<Product>)products;
            },
            cancellationToken);

    public Task<Product?> GetByExternalIdAsync(
        string providerName,
        string externalId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        return ReadAsync(
            async db => await Query(db)
                .Include(p => p.ExternalMappings)
                .FirstOrDefaultAsync(
                    p => p.ExternalMappings.Any(m => m.ProviderName == providerName && m.ExternalId == externalId),
                    cancellationToken)
                .ConfigureAwait(false),
            cancellationToken);
    }
}
