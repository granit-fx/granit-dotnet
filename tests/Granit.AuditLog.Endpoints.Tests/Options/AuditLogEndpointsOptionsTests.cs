using Granit.AuditLog.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.AuditLog.Endpoints.Tests.Options;

public sealed class AuditLogEndpointsOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        AuditLogEndpointsOptions options = new();

        options.RoutePrefix.ShouldBe("audit-log");
        options.TagName.ShouldBe("Audit Log");
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        AuditLogEndpointsOptions options = new()
        {
            RoutePrefix = "custom-audit",
            TagName = "Custom Tag",
        };

        options.RoutePrefix.ShouldBe("custom-audit");
        options.TagName.ShouldBe("Custom Tag");
    }
}
