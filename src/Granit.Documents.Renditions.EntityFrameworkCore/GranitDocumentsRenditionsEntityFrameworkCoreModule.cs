using Granit.Documents.Renditions;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Documents.Renditions.EntityFrameworkCore;

/// <summary>
/// Granit module for the renditions EF Core persistence layer. Owns
/// <c>RenditionsDbContext</c> and <c>RenditionStore</c>; subscribes to
/// <c>DocumentPermanentlyDeletedEvent</c> through the Wolverine handler in
/// <c>Internal.DocumentPermanentlyDeletedRenditionHandler</c>.
/// </summary>
[DependsOn(
    typeof(GranitDocumentsRenditionsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitDocumentsRenditionsEntityFrameworkCoreModule : GranitModule;
