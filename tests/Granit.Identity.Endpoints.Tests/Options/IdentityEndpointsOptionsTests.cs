using Granit.Identity.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Options;

public sealed class IdentityEndpointsOptionsTests
{
    [Fact]
    public void SectionName_IsIdentityEndpoints() => IdentityEndpointsOptions.SectionName.ShouldBe("IdentityEndpoints");

    [Fact]
    public void Defaults_RoutePrefixIsIdentityUsers()
    {
        IdentityEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("identity/users");
    }

    [Fact]
    public void Defaults_TagNameIsIdentityUserCache()
    {
        IdentityEndpointsOptions options = new();

        options.TagName.ShouldBe("Identity User Cache");
    }

    [Fact]
    public void Defaults_RequiredRoleIsGranitIdentityAdmin()
    {
        IdentityEndpointsOptions options = new();

        options.RequiredRole.ShouldBe("granit-identity-admin");
    }

    [Fact]
    public void Properties_AreSettable()
    {
        IdentityEndpointsOptions options = new()
        {
            RoutePrefix = "custom/prefix",
            TagName = "Custom Tag",
            RequiredRole = "custom-role",
        };

        options.RoutePrefix.ShouldBe("custom/prefix");
        options.TagName.ShouldBe("Custom Tag");
        options.RequiredRole.ShouldBe("custom-role");
    }
}
