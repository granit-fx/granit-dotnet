using Granit.Modularity;

namespace Granit.BlobStorage.Proxy;

/// <summary>
/// Granit module for the blob storage reverse proxy.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitBlobStorageProxy()</c>.
/// Exposes proxy endpoints via <c>MapGranitBlobStorageProxy()</c>.
/// </remarks>
[DependsOn(typeof(GranitBlobStorageModule))]
public sealed class GranitBlobStorageProxyModule : GranitModule;
