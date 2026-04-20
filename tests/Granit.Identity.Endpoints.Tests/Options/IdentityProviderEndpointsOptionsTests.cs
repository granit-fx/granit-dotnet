using Granit.Identity.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Options;

public sealed class IdentityProviderEndpointsOptionsTests
{
    [Fact]
    public void SectionName_IsIdentityProviderEndpoints() => IdentityProviderEndpointsOptions.SectionName.ShouldBe("IdentityProviderEndpoints");

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

    [Fact]
    public void Properties_AreSettable()
    {
        IdentityProviderEndpointsOptions options = new()
        {
            RoutePrefix = "admin/identity",
            TagName = "Admin Identity",
        };

        options.RoutePrefix.ShouldBe("admin/identity");
        options.TagName.ShouldBe("Admin Identity");
    }
}
