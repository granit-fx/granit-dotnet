using Granit.MultiTenancy.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Endpoints.Tests.Options;

public sealed class MultiTenancyEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        MultiTenancyEndpointsOptions.SectionName.ShouldBe("MultiTenancy:Endpoints");

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        MultiTenancyEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("multi-tenancy");
        options.TagName.ShouldBe("Multi-Tenancy - Tenants");
    }
}
