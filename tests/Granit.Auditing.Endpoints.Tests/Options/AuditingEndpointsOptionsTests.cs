using Granit.Auditing.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Endpoints.Tests.Options;

public sealed class AuditingEndpointsOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditingEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("audit-log");
        options.TagName.ShouldBe("Audit Log");
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        AuditingEndpointsOptions options = new()
        {
            RoutePrefix = "custom-audit",
            TagName = "Custom Tag",
        };

        options.RoutePrefix.ShouldBe("custom-audit");
        options.TagName.ShouldBe("Custom Tag");
    }
}
