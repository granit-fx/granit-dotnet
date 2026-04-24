using Granit.Catalog.Domain;
using Granit.Catalog.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Catalog.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for the Granit catalog (Product aggregate).
/// </summary>
internal sealed class CatalogDbContext(
    DbContextOptions<CatalogDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<Product> Products { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureCatalogModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
