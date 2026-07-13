using Granit.Diagnostics.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Endpoints.Tests;

public sealed class SecurityHeadersAuditOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        SecurityHeadersAuditOptions.SectionName.ShouldBe("Diagnostics:Endpoints:SecurityHeadersAudit");
}
