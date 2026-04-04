using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.CustomerBalance.EntityFrameworkCore;

/// <summary>EF Core persistence for Granit.CustomerBalance.</summary>
[DependsOn(
    typeof(GranitCustomerBalanceModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitCustomerBalanceEntityFrameworkCoreModule : GranitModule;
