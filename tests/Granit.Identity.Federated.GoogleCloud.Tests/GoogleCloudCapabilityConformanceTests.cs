using Granit.Identity.Federated.GoogleCloud.Internal;
using Granit.Testing.IdentityProviders;
using Xunit;

namespace Granit.Identity.Federated.GoogleCloud.Tests;

public sealed class GoogleCloudCapabilityConformanceTests
{
    [Fact]
    public void Capabilities_are_honest() =>
        IdentityProviderCapabilityConformance.AssertConforms(
            new GoogleCloudIdentityProviderCapabilities(),
            typeof(GoogleCloudIdentityProvider));
}
