using Granit.Authorization.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Endpoints.Tests;

public sealed class AuthorizationEndpointsOptionsTests
{
    [Fact]
    public void RoutePrefix_Default_ShouldBeAuth() =>
        new AuthorizationEndpointsOptions().RoutePrefix.ShouldBe("authorization");

    [Fact]
    public void TagName_Default_ShouldBeAuthorization() =>
        new AuthorizationEndpointsOptions().TagName.ShouldBe("Authorization");
}
