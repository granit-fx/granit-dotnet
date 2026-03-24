using Granit.BlobStorage.FileSystem.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.FileSystem.Internal;

/// <summary>
/// <see cref="IBlobKeyStrategy"/> using tenant-prefix isolation on the local file system.
/// </summary>
/// <remarks>
/// <see cref="ResolveBucketName"/> always returns <see cref="FileSystemBlobOptions.BasePath"/>
/// since the file system provider uses a single root directory.
/// </remarks>
internal sealed class FileSystemBlobKeyStrategy(
    ICurrentTenant currentTenant,
    IClock clock,
    IOptions<FileSystemBlobOptions> options) : TenantPrefixBlobKeyStrategy(currentTenant, clock)
{
    /// <inheritdoc/>
    public override string ResolveBucketName(string containerName) => options.Value.BasePath;
}
