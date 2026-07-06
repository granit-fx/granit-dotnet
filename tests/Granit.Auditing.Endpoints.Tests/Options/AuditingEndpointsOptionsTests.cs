using Granit.Auditing.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Options;

public sealed class AuditingEndpointsOptionsTests
{
    [Fact]
    public void SectionName_HasExpectedValue() =>
        AuditingEndpointsOptions.SectionName.ShouldBe("Auditing:Endpoints");

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditingEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("auditing");
        options.TagName.ShouldBe("Audit Log");
    }
}
