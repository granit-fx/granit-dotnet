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

    [Fact]
    public void AccountRoutePrefix_CanBeCustomized()
    {
        AccountEndpointsOptions options = new()
        {
            AccountRoutePrefix = "custom/account",
        };

        options.AccountRoutePrefix.ShouldBe("custom/account");
    }

    [Fact]
    public void AdminRoutePrefix_CanBeCustomized()
    {
        AccountEndpointsOptions options = new()
        {
            AdminRoutePrefix = "custom/admin",
        };

        options.AdminRoutePrefix.ShouldBe("custom/admin");
    }

    [Fact]
    public void AllProperties_CanBeCustomized()
    {
        AccountEndpointsOptions options = new()
        {
            AccountRoutePrefix = "v2/account",
            AdminRoutePrefix = "v2/admin",
        };

        options.AccountRoutePrefix.ShouldBe("v2/account");
        options.AdminRoutePrefix.ShouldBe("v2/admin");
    }
}
