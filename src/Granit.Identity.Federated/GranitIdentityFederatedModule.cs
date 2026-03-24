using Granit.Identity;
using Granit.Modularity;

namespace Granit.Identity.Federated;

/// <summary>
/// Granit module providing federated identity abstractions.
/// Groups the shared dependency for all federated identity providers
/// (Keycloak, Entra ID, Cognito, Google Cloud).
/// </summary>
[DependsOn(typeof(GranitIdentityModule))]
public sealed class GranitIdentityFederatedModule : GranitModule;
