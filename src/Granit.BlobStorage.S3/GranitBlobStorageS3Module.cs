using Granit.Modularity;

namespace Granit.BlobStorage.S3;

/// <summary>
/// Granit module for the S3-compatible blob storage provider.
/// </summary>
/// <remarks>
/// Registers <c>S3BlobClient</c> as both <c>IBlobStoreProvider</c> and
/// <c>IPresignedUrlProvider</c> when <see cref="Extensions.BlobStorageS3HostApplicationBuilderExtensions.AddGranitBlobStorageS3"/>
/// is called.
/// </remarks>
[DependsOn(typeof(GranitBlobStorageModule))]
public sealed class GranitBlobStorageS3Module : GranitModule;
