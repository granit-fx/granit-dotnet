using Granit.Dashboards;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Permissions;

/// <summary>
/// Locks the canonical permission constants and the
/// <c>[Group].[Resource].[Action]</c> three-segment shape required by the
/// framework's permission convention archi tests.
/// </summary>
public sealed class DashboardsPermissionsTests
{
    [Fact]
    public void Group_IsDashboards()
        => DashboardsPermissions.GroupName.ShouldBe("Dashboards");

    [Fact]
    public void Catalog_Read_FollowsThreeSegmentFormat()
        => DashboardsPermissions.Catalog.Read.ShouldBe("Dashboards.Catalog.Read");

    [Fact]
    public void Catalog_Read_HasExactlyThreeDots()
    {
        // Three-segment format = exactly two dots between segments.
        DashboardsPermissions.Catalog.Read.Split('.').Length.ShouldBe(3);
    }
}
