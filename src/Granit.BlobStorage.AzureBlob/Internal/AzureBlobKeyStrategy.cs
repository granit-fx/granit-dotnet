using Granit.BlobStorage.AzureBlob.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.AzureBlob.Internal;

/// <summary>
/// Default <see cref="IBlobKeyStrategy"/> using tenant-prefix isolation for Azure Blob Storage.
/// </summary>
/// <remarks>
/// The date components improve list-operation performance by distributing
/// blobs across a wider virtual directory hierarchy.
/// </remarks>
internal sealed class AzureBlobKeyStrategy(
    ICurrentTenant currentTenant,
    IClock clock,
    IOptions<AzureBlobOptions> options) : TenantPrefixBlobKeyStrategy(currentTenant, clock)
{
    /// <inheritdoc/>
    public override string ResolveBucketName(string containerName) => options.Value.DefaultContainer;
}
