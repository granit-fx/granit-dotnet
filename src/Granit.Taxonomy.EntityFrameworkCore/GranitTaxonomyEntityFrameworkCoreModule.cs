using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Taxonomy.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of <c>Granit.Taxonomy</c>.
/// </summary>
/// <remarks>
/// Provides the isolated <c>TaxonomyDbContext</c> and the <c>ConfigureTaxonomyModule</c>
/// model-builder extension. The DbContext is registered via the host extension
/// <c>AddGranitTaxonomyEntityFrameworkCore(...)</c>; this module class only declares the
/// dependency graph for the Granit module system.
/// </remarks>
[DependsOn(
    typeof(GranitTaxonomyModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitTaxonomyEntityFrameworkCoreModule : GranitModule;
