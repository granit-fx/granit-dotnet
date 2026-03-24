using Granit.Modularity;

namespace Granit.BlobStorage.GoogleCloud;

/// <summary>
/// Granit module for the Google Cloud Storage blob storage provider.
/// </summary>
/// <remarks>
/// Registers <c>GoogleCloudBlobClient</c> as both <c>IBlobStoreProvider</c> and
/// <c>IPresignedUrlProvider</c> when <see cref="Extensions.BlobStorageGoogleCloudHostApplicationBuilderExtensions.AddGranitBlobStorageGoogleCloud"/>
/// is called.
/// </remarks>
[DependsOn(typeof(GranitBlobStorageModule))]
public sealed class GranitBlobStorageGoogleCloudModule : GranitModule;
