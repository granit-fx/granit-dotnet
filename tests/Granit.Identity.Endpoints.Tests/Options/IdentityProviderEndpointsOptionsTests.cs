using Granit.Identity.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Options;

public sealed class IdentityProviderEndpointsOptionsTests
{
    [Fact]
    public void SectionName_IsIdentityProviderEndpoints() => IdentityProviderEndpointsOptions.SectionName.ShouldBe("Identity:Endpoints:Provider");

    [Fact]
    public void Defaults_RoutePrefixIsIdentityProvider()
    {
        IdentityProviderEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("identity/provider");
    }

    [Fact]
    public void Defaults_TagNameIsIdentityProvider()
    {
        IdentityProviderEndpointsOptions options = new();

        options.TagName.ShouldBe("Identity - Provider");
    }
}
