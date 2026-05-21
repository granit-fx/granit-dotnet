using Granit.Http.SecurityHeaders.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.SecurityHeaders.Endpoints.Tests.Options;

public sealed class SecurityHeadersEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        SecurityHeadersEndpointsOptions.SectionName.ShouldBe("Http:SecurityHeaders:Endpoints");
}
