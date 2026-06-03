using Granit.Identity.Federated.Privacy.DataExport;
using Granit.Privacy;
using Granit.Privacy.BlobStorage.Extensions;

namespace Granit.Identity.Federated.Privacy.Extensions;

/// <summary>
/// <see cref="GranitPrivacyBuilder"/> extensions that register the federated identity
/// privacy provider.
/// </summary>
public static class PrivacyBuilderIdentityFederatedExtensions
{
    /// <summary>
    /// Registers <see cref="IdentityFederatedPrivacyDataProvider"/> and adds
    /// <c>"identity-federated"</c> to the scatter-gather registry.
    /// </summary>
    /// <remarks>
    /// The matching Wolverine handler is discovered automatically. This call also wires the
    /// <c>Granit.Privacy.BlobStorage</c> staging infrastructure the provider depends on
    /// (<c>IStagedFragmentBuilder</c>, uploader, assembler) — registration is idempotent
    /// (<c>TryAdd</c>), so the host no longer needs an explicit
    /// <c>[DependsOn(GranitPrivacyBlobStorageModule)]</c>. The provider still requires
    /// <c>GranitIdentityFederatedModule</c> (for <c>IFederatedUserCacheReader</c>) to be loaded.
    /// </remarks>
    public static GranitPrivacyBuilder AddGranitIdentityFederatedPrivacyProvider(
        this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddGranitPrivacyBlobStorage();
        return builder.AddDataProvider<IdentityFederatedPrivacyDataProvider>();
    }
}
