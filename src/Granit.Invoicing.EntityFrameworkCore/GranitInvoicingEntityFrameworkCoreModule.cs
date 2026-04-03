using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Invoicing.EntityFrameworkCore;

/// <summary>EF Core persistence for Granit.Invoicing.</summary>
[DependsOn(
    typeof(GranitInvoicingModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitInvoicingEntityFrameworkCoreModule : GranitModule;
