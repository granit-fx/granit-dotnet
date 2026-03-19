using Granit.Core.Modularity;
using Granit.Wolverine;

namespace Granit.BlobStorage.Wolverine;

/// <summary>
/// Granit module for Wolverine-based blob storage background jobs.
/// Registers the <see cref="CleanupOrphanBlobsCommand"/> recurring job (hourly).
/// </summary>
[DependsOn(
    typeof(GranitBlobStorageModule),
    typeof(GranitWolverineModule))]
public sealed class GranitBlobStorageWolverineModule : GranitModule;
