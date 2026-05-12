using Granit.BlobStorage;
using Granit.Caching;
using Granit.Modularity;
using Granit.Taxonomy;

namespace Granit.Documents;

/// <summary>
/// Granit module for user-managed file and asset management on top of
/// <see cref="GranitBlobStorageModule"/>.
/// </summary>
/// <remarks>
/// <para>
/// Composes <see cref="GranitBlobStorageModule"/> for binary storage; every
/// <c>DocumentVersion</c> references one <c>BlobDescriptor</c>. <c>Granit.Documents</c>
/// adds folder hierarchy, ownership, share ACL with path-based resolution, autonomous
/// versioning, tenant storage quota, and tags.
/// </para>
/// <para>
/// Phase 1 ships only the scaffolding declared in
/// <see cref="Microsoft.Extensions.DependencyInjection.DocumentsServiceCollectionExtensions.AddGranitDocuments(Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action{Options.GranitDocumentsOptions}?)"/>.
/// Domain types, endpoints, and persistence are introduced in subsequent stories
/// of the tracking Epic.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitBlobStorageModule),
    typeof(GranitCachingModule),
    typeof(GranitTaxonomyModule))]
public sealed class GranitDocumentsModule : GranitModule;
