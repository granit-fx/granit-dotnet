using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Catalog.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of the catalog (Product aggregate).
/// </summary>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitCatalogModule))]
public sealed class GranitCatalogEntityFrameworkCoreModule : GranitModule;
