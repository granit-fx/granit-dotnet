using Granit.Identity.Local.Privacy.DataExport;
using Granit.Privacy;
using Granit.Privacy.BlobStorage.Extensions;

namespace Granit.Identity.Local.Privacy.Extensions;

/// <summary>
/// <see cref="GranitPrivacyBuilder"/> extensions that plug the built-in
/// <see cref="IdentityLocalPrivacyDataProvider"/> into the scatter-gather registry.
/// </summary>
public static class PrivacyBuilderIdentityLocalExtensions
{
    /// <summary>
    /// Registers <see cref="IdentityLocalPrivacyDataProvider"/> as an
    /// <c>IPrivacyDataProvider</c> and adds <c>"identity-local"</c> to the list of providers
    /// the export saga expects fragments from.
    /// </summary>
    /// <remarks>
    /// The matching Wolverine handler is discovered automatically. This call also wires the
    /// <c>Granit.Privacy.BlobStorage</c> staging infrastructure the provider depends on
    /// (<c>IStagedFragmentBuilder</c>, uploader, assembler) — registration is idempotent
    /// (<c>TryAdd</c>), so the host no longer needs an explicit
    /// <c>[DependsOn(GranitPrivacyBlobStorageModule)]</c>. The provider still requires the
    /// <c>Granit.Identity.Local</c> identity stack (for <c>UserManager&lt;LocalIdentity&gt;</c>) to be loaded.
    /// </remarks>
    public static GranitPrivacyBuilder AddGranitIdentityLocalPrivacyProvider(
        this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddGranitPrivacyBlobStorage();
        return builder.AddDataProvider<IdentityLocalPrivacyDataProvider>();
    }
}
