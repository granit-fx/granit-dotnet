using Granit.Identity.Federated.EntraId.Internal;
using Granit.Testing.IdentityProviders;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class EntraIdCapabilityConformanceTests
{
    [Fact]
    public void Capabilities_are_honest() =>
        IdentityProviderCapabilityConformance.AssertConforms(
            new EntraIdIdentityProviderCapabilities(),
            typeof(EntraIdIdentityProvider));
}
