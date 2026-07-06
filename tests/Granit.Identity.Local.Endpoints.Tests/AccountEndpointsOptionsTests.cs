using Granit.Identity.Local.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests;

public sealed class AccountEndpointsOptionsTests
{
    [Fact]
    public void AccountRoutePrefix_Default_IsApiAccount()
    {
        AccountEndpointsOptions options = new();

        options.AccountRoutePrefix.ShouldBe("account");
    }

    [Fact]
    public void AdminRoutePrefix_Default_IsApiAdmin()
    {
        AccountEndpointsOptions options = new();

        options.AdminRoutePrefix.ShouldBe("admin");
    }
}
