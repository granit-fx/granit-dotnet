using Granit.Encryption;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.EntityMerge.EntityFrameworkCore;

/// <summary>
/// Granit module marker for the EF Core merge orchestrator package. Wired via
/// <c>builder.AddGranitEntityMergeEntityFrameworkCore(...)</c>.
/// </summary>
[DependsOn(
    typeof(GranitEncryptionModule),
    typeof(GranitEntityMergeModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitEntityMergeEntityFrameworkCoreModule : GranitModule;
