using Granit.BlobStorage;
using Granit.Modularity;
using Granit.Validation;

namespace Granit.Privacy.BlobStorage.Endpoints;

/// <summary>
/// Marker module for the blob-backed privacy export download endpoint. The endpoint itself is
/// mapped explicitly via <c>MapGranitPrivacyExportDownload()</c>; this module only anchors the
/// dependency chain (validation auto-filter, privacy abstractions, blob storage) for hosts that
/// wire modules by <c>[DependsOn]</c> discovery.
/// </summary>
[DependsOn(typeof(GranitBlobStorageModule))]
[DependsOn(typeof(GranitPrivacyModule))]
[DependsOn(typeof(GranitValidationModule))]
public sealed class GranitPrivacyBlobStorageEndpointsModule : GranitModule;
