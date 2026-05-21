using Granit.Timeline.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Endpoints.Tests;

/// <summary>
/// Unit tests for <see cref="TimelineEndpointsOptions"/> default values.
/// </summary>
public sealed class TimelineEndpointsOptionsTests
{
    [Fact]
    public void SectionName_is_TimelineEndpoints() =>
        TimelineEndpointsOptions.SectionName.ShouldBe("Timeline:Endpoints");

    [Fact]
    public void Default_RoutePrefix_is_timeline()
    {
        TimelineEndpointsOptions options = new();
        options.RoutePrefix.ShouldBe("timeline");
    }

    [Fact]
    public void Default_TagName_is_Timeline()
    {
        TimelineEndpointsOptions options = new();
        options.TagName.ShouldBe("Timeline");
    }

    [Fact]
    public void Properties_are_mutable()
    {
        TimelineEndpointsOptions options = new()
        {
            RoutePrefix = "audit-trail",
            TagName = "AuditTrail",
        };

        options.RoutePrefix.ShouldBe("audit-trail");
        options.TagName.ShouldBe("AuditTrail");
    }
}
