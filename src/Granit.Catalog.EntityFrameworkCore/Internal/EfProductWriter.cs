using Granit.Catalog.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Granit.Catalog.EntityFrameworkCore.Internal;

internal sealed class EfProductWriter(
    IDbContextFactory<CatalogDbContext> contextFactory)
    : EfStoreBase<Product, CatalogDbContext>(contextFactory),
      IProductWriter
{
    Task IProductWriter.AddAsync(Product product, CancellationToken cancellationToken) =>
        base.AddAsync(product, cancellationToken);

    Task IProductWriter.UpdateAsync(Product product, CancellationToken cancellationToken) =>
        base.WriteAsync(async db =>
        {
            // DbSet.Update() marks the entire disconnected graph as Modified.
            // ProductExternalMapping entries added in memory (via AddExternalMapping)
            // don't exist in the database yet — flip them to Added so EF inserts
            // instead of issuing UPDATEs against non-existent rows.
            var existingMappingIds = (await db.Set<ProductExternalMapping>()
                .AsNoTracking()
                .Where(m => EF.Property<Guid>(m, "ProductId") == product.Id)
                .Select(m => m.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
                .ToHashSet();

            db.Set<Product>().Update(product);

            IEnumerable<EntityEntry<ProductExternalMapping>> orphans = db.ChangeTracker
                .Entries<ProductExternalMapping>()
                .Where(entry => entry.State == EntityState.Modified
                                && !existingMappingIds.Contains(entry.Entity.Id));

            foreach (EntityEntry<ProductExternalMapping> entry in orphans)
            {
                entry.State = EntityState.Added;
            }
        }, cancellationToken);
}
