using Granit.Identity.Federated.Keycloak.Internal;
using Granit.Testing.IdentityProviders;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests;

public sealed class KeycloakCapabilityConformanceTests
{
    [Fact]
    public void Capabilities_are_honest() =>
        IdentityProviderCapabilityConformance.AssertConforms(
            new KeycloakIdentityProviderCapabilities(),
            typeof(KeycloakIdentityProvider));
}
