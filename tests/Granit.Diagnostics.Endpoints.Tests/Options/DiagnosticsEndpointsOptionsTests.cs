using Granit.Diagnostics.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Endpoints.Tests.Options;

public sealed class DiagnosticsEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        DiagnosticsEndpointsOptions.SectionName.ShouldBe("Diagnostics:Endpoints");
}
