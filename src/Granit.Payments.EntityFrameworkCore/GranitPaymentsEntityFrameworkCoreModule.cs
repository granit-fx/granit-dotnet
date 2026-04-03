using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Payments.EntityFrameworkCore;

/// <summary>EF Core persistence for Granit.Payments.</summary>
[DependsOn(
    typeof(GranitPaymentsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitPaymentsEntityFrameworkCoreModule : GranitModule;
