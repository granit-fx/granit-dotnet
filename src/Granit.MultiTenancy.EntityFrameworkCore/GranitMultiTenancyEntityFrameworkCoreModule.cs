using Granit.DataLookup.EntityFrameworkCore;
using Granit.Events;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.QueryEngine;

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
    typeof(GranitDataLookupEntityFrameworkCoreModule),
    typeof(GranitEventsModule),
    typeof(GranitMultiTenancyModule),
    typeof(GranitPersistenceEntityFrameworkCoreMigrationsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitQueryEngineModule))]
public sealed class GranitMultiTenancyEntityFrameworkCoreModule : GranitModule;
