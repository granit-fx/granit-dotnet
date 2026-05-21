using Granit.Identity.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests.Options;

public sealed class IdentityEndpointsOptionsTests
{
    [Fact]
    public void SectionName_IsIdentityEndpoints() => IdentityEndpointsOptions.SectionName.ShouldBe("Identity:Endpoints");

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

        options.TagName.ShouldBe("Identity - User Cache");
    }

    [Fact]
    public void Properties_AreSettable()
    {
        IdentityEndpointsOptions options = new()
        {
            RoutePrefix = "custom/prefix",
            TagName = "Custom Tag",
        };

        options.RoutePrefix.ShouldBe("custom/prefix");
        options.TagName.ShouldBe("Custom Tag");
    }
}
