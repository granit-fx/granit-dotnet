using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Presence.EntityFrameworkCore;

/// <summary>
/// Granit module that enables EF Core persistence for <c>Granit.Presence</c>.
/// </summary>
/// <remarks>
/// Replaces the default in-memory <c>IPresenceStore</c> with a durable PostgreSQL
/// implementation. The application must configure the DbContext via
/// <c>AddGranitPresenceEntityFrameworkCore(opts =&gt; opts.UseNpgsql(connectionString))</c>.
/// </remarks>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitPresenceModule))]
public sealed class GranitPresenceEntityFrameworkCoreModule : GranitModule;
