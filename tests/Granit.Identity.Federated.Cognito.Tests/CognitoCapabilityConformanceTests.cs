using Granit.Identity.Federated.Cognito.Internal;
using Granit.Testing.IdentityProviders;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests;

public sealed class CognitoCapabilityConformanceTests
{
    [Fact]
    public void Capabilities_are_honest() =>
        IdentityProviderCapabilityConformance.AssertConforms(
            new CognitoIdentityProviderCapabilities(),
            typeof(CognitoIdentityProvider));
}
