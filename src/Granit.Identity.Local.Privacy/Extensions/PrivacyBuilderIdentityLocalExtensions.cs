using Granit.Identity.Local.Privacy.DataExport;
using Granit.Privacy;

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
    /// The matching Wolverine handler is discovered automatically. Requires
    /// <see cref="GranitIdentityLocalPrivacyModule"/> to be loaded.
    /// </remarks>
    public static GranitPrivacyBuilder AddGranitIdentityLocalPrivacyProvider(
        this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddDataProvider<IdentityLocalPrivacyDataProvider>();
    }
}
