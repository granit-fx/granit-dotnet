using Granit.Identity.Federated.Wolverine.DataExport;
using Granit.Privacy;

namespace Granit.Identity.Federated.Wolverine.Extensions;

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
    /// The matching Wolverine handler is discovered automatically. Requires
    /// <see cref="GranitIdentityFederatedWolverineModule"/> to be loaded.
    /// </remarks>
    public static GranitPrivacyBuilder AddGranitIdentityFederatedPrivacyProvider(
        this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddDataProvider<IdentityFederatedPrivacyDataProvider>();
    }
}
