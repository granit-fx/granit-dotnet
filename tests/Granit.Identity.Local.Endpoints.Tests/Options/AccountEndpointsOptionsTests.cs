using Granit.Identity.Local.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests.Options;

public sealed class AccountEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        AccountEndpointsOptions.SectionName.ShouldBe("Identity:Local:Endpoints:Account");
}
