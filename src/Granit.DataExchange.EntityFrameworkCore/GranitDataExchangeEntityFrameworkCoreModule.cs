using Granit.Core.Modularity;
using Granit.Persistence;

namespace Granit.DataExchange.EntityFrameworkCore;

/// <summary>
/// Module for the EF Core persistence layer of <c>Granit.DataExchange</c>.
/// Provides <c>DataExchangeDbContext</c>, mapping store, import job store,
/// identity resolvers, and batched import executor.
/// Tables are managed by the host application's migrations
/// (via <c>modelBuilder.ConfigureDataExchangeModule()</c>), like all other modules.
/// </summary>
[DependsOn(typeof(GranitDataExchangeModule))]
[DependsOn(typeof(GranitPersistenceModule))]
public sealed class GranitDataExchangeEntityFrameworkCoreModule : GranitModule;
