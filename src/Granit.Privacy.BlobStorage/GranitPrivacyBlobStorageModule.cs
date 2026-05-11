using Granit.BlobStorage;
using Granit.IO;
using Granit.Modularity;
using Granit.Privacy.BlobStorage.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Privacy.BlobStorage;

/// <summary>
/// Bridges <c>Granit.Privacy</c> personal-data exports to <c>Granit.BlobStorage</c>.
/// Registers <c>PrivacyFragmentUploader</c> — the shared upload-and-publish utility used
/// by every <c>IPrivacyDataProvider</c> Wolverine handler in the scatter-gather saga.
/// </summary>
[DependsOn(typeof(GranitBlobStorageModule))]
[DependsOn(typeof(GranitIOModule))]
[DependsOn(typeof(GranitPrivacyModule))]
public sealed class GranitPrivacyBlobStorageModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitPrivacyBlobStorage();
}
