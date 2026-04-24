using Microsoft.EntityFrameworkCore;

namespace Granit.Catalog.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit catalog entity configurations.
/// </summary>
public static class CatalogModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Granit Catalog module.</summary>
    public static ModelBuilder ConfigureCatalogModule(this ModelBuilder modelBuilder)
    {
        // Entity configurations (Product, ProductExternalMapping) will be applied
        // here once they exist (commit 4 — persistence).
        return modelBuilder;
    }
}
