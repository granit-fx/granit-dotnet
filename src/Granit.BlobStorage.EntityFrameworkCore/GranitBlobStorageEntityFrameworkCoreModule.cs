using Granit.Modularity;
using Granit.Persistence;

namespace Granit.BlobStorage.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of blob descriptors.
/// Registers <c>BlobStorageDbContext</c> and <c>EfBlobDescriptorStore</c>.
/// </summary>
[DependsOn(
    typeof(GranitBlobStorageModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitBlobStorageEntityFrameworkCoreModule : GranitModule;
