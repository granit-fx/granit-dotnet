using Granit.Identity.Local.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests.Options;

public sealed class RoleEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        RoleEndpointsOptions.SectionName.ShouldBe("Identity:Local:Endpoints:Role");
}
