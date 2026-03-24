using Granit.Modularity;

namespace Granit.BlobStorage.FileSystem;

/// <summary>
/// Granit module for the local file system blob storage provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitBlobStorageFileSystem()</c>.
/// Pre-signed URLs require <c>Granit.BlobStorage.Proxy</c>.
/// </remarks>
[DependsOn(typeof(GranitBlobStorageModule))]
public sealed class GranitBlobStorageFileSystemModule : GranitModule;
