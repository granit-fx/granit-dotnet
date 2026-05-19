using Granit.BlobStorage;
using Granit.DataExchange.BlobStorage.Extensions;
using Granit.Modularity;

namespace Granit.DataExchange.BlobStorage;

/// <summary>
/// Bridges <c>Granit.DataExchange</c> file operations to <c>Granit.BlobStorage</c>.
/// Replaces the default in-memory <see cref="IDataExchangeFileProvider"/> with a
/// concrete implementation backed by the registered <c>IBlobStoreProvider</c>.
/// </summary>
[DependsOn(typeof(GranitDataExchangeModule))]
[DependsOn(typeof(GranitBlobStorageModule))]
public sealed class GranitDataExchangeBlobStorageModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDataExchangeBlobStorage();
}
