using Granit.Encryption.EntityFrameworkCore;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Activities.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence in the activities runtime. Pulls in
/// <see cref="GranitActivitiesModule"/> + <see cref="GranitPersistenceEntityFrameworkCoreModule"/>;
/// the host application wires the actual provider via
/// <c>AddGranitActivitiesEntityFrameworkCore(opts =&gt; opts.UseNpgsql(...))</c>.
/// </summary>
[DependsOn(
    typeof(GranitActivitiesModule),
    typeof(GranitEncryptionEntityFrameworkCoreModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitActivitiesEntityFrameworkCoreModule : GranitModule;
