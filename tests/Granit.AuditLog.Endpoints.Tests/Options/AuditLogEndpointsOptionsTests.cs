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
        options.AuthorizationPolicy.ShouldBe("AuditLog.Entries.Read");
        options.RequiredRole.ShouldBe("granit-audit-log-admin");
        options.TagName.ShouldBe("Audit Log");
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        AuditLogEndpointsOptions options = new()
        {
            RoutePrefix = "custom-audit",
            AuthorizationPolicy = "Custom.Policy",
            RequiredRole = "custom-role",
            TagName = "Custom Tag",
        };

        options.RoutePrefix.ShouldBe("custom-audit");
        options.AuthorizationPolicy.ShouldBe("Custom.Policy");
        options.RequiredRole.ShouldBe("custom-role");
        options.TagName.ShouldBe("Custom Tag");
    }
}
