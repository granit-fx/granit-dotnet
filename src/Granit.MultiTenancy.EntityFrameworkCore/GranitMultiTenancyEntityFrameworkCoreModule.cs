using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.MultiTenancy.EntityFrameworkCore;

/// <summary>
/// Granit module for multi-tenancy EF Core persistence.
/// </summary>
/// <remarks>
/// Provides the <c>Tenant</c> aggregate root, <c>MultiTenancyDbContext</c>, and
/// <c>EfCoreTenantStore</c> implementing <see cref="Stores.ITenantReader"/> and
/// <see cref="Stores.ITenantWriter"/>.
/// Registered via <c>AddGranitMultiTenancyEntityFrameworkCore(opt => opt.UseNpgsql(...))</c>.
/// </remarks>
[DependsOn(
    typeof(GranitMultiTenancyModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitMultiTenancyEntityFrameworkCoreModule : GranitModule;
