using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.BlobStorage.Database;

/// <summary>
/// Granit module for the database blob storage provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitBlobStorageDatabase(configure)</c>.
/// Pre-signed URLs require <c>Granit.BlobStorage.Proxy</c>.
/// </remarks>
[DependsOn(typeof(GranitBlobStorageModule))]
[DependsOn(typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitBlobStorageDatabaseModule : GranitModule;
