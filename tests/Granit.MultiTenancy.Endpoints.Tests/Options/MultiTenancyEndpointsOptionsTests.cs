using Granit.MultiTenancy.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests.Options;

public sealed class MultiTenancyEndpointsOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        MultiTenancyEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("admin/tenants");
        options.TagName.ShouldBe("Platform - Tenants");
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        MultiTenancyEndpointsOptions options = new()
        {
            RoutePrefix = "custom-tenants",
            TagName = "Custom Tag",
        };

        options.RoutePrefix.ShouldBe("custom-tenants");
        options.TagName.ShouldBe("Custom Tag");
    }
}
