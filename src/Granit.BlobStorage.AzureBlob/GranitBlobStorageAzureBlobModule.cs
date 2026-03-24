using Granit.Modularity;

namespace Granit.BlobStorage.AzureBlob;

/// <summary>
/// Granit module for Azure Blob Storage provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitBlobStorageAzureBlob()</c>.
/// </remarks>
[DependsOn(typeof(GranitBlobStorageModule))]
public sealed class GranitBlobStorageAzureBlobModule : GranitModule;
