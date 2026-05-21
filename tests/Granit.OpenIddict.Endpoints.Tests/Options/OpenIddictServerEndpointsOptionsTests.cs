using Granit.OpenIddict.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests.Options;

public sealed class OpenIddictServerEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        OpenIddictServerEndpointsOptions.SectionName.ShouldBe("OpenIddict:Server:Endpoints");
}
