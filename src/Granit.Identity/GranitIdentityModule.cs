using Granit.Identity.Extensions;
using Granit.Modularity;
using Granit.Querying;

namespace Granit.Identity;

/// <summary>
/// Granit module for identity provider abstractions.
/// Registers a <see cref="NullIdentityProvider"/> by default.
/// Install a provider package (e.g. <c>Granit.Identity.Federated.Keycloak</c>) to connect
/// to a real identity system.
/// </summary>
[DependsOn(typeof(GranitQueryingModule))]
public sealed class GranitIdentityModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitIdentity();
}
