using Granit.Modularity;
using Granit.Persistence;

namespace Granit.BlobStorage.Database;

/// <summary>
/// Granit module for the database blob storage provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitBlobStorageDbStore(configure)</c>.
/// Pre-signed URLs require <c>Granit.BlobStorage.Proxy</c>.
/// </remarks>
[DependsOn(typeof(GranitBlobStorageModule))]
[DependsOn(typeof(GranitPersistenceModule))]
public sealed class GranitBlobStorageDbStoreModule : GranitModule;
