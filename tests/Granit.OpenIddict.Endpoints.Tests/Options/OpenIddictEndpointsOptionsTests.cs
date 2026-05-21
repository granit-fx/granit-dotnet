using Granit.OpenIddict.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Endpoints.Tests.Options;

public sealed class OpenIddictEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        OpenIddictEndpointsOptions.SectionName.ShouldBe("OpenIddict:Endpoints");
}
