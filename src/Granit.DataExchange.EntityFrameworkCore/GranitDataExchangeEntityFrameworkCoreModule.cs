using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore;

/// <summary>
/// Module for the EF Core persistence layer of <c>Granit.DataExchange</c>.
/// Provides <c>DataExchangeDbContext</c>, mapping store, import job store,
/// identity resolvers (business key, composite key, external ID),
/// and batched import executor.
/// Tables are managed by the host application's migrations
/// (via <c>modelBuilder.ConfigureDataExchangeModule()</c>), like all other modules.
/// </summary>
[DependsOn(
    typeof(GranitDataExchangeModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitDataExchangeEntityFrameworkCoreModule : GranitModule;
