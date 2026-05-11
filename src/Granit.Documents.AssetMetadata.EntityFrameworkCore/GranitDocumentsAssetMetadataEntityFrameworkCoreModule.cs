using Granit.Documents.AssetMetadata;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore;

/// <summary>
/// Granit module for the asset-metadata EF Core persistence layer. Owns
/// <c>AssetMetadataDbContext</c> and <c>AssetMetadataStore</c>; subscribes to
/// <c>DocumentPermanentlyDeletedEvent</c> through the Wolverine handler in
/// <c>Internal.DocumentPermanentlyDeletedAssetMetadataHandler</c>.
/// </summary>
[DependsOn(
    typeof(GranitDocumentsAssetMetadataModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitDocumentsAssetMetadataEntityFrameworkCoreModule : GranitModule;
