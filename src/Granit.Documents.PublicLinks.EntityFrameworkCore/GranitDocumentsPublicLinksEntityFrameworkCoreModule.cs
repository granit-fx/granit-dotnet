using Granit.Documents.PublicLinks;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore;

/// <summary>
/// Granit module for the public-links EF Core persistence layer (F18.2). Owns
/// <c>DocumentsPublicLinksDbContext</c>, <c>EfDocumentPublicLinkStore</c> and
/// <c>DocumentPublicLinkService</c>.
/// </summary>
[DependsOn(
    typeof(GranitDocumentsPublicLinksModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitDocumentsPublicLinksEntityFrameworkCoreModule : GranitModule;
