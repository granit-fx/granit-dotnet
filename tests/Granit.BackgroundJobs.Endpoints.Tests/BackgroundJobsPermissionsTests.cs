using Granit.BackgroundJobs.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Endpoints.Tests;

public sealed class BackgroundJobsPermissionsTests
{
    [Fact]
    public void GroupName_IsBackgroundJobs() => BackgroundJobsPermissions.GroupName.ShouldBe("BackgroundJobs");

    [Fact]
    public void Jobs_Read_FollowsThreeSegmentConvention() => BackgroundJobsPermissions.Jobs.Read.ShouldBe("BackgroundJobs.Jobs.Read");

    [Fact]
    public void Jobs_Manage_FollowsThreeSegmentConvention() => BackgroundJobsPermissions.Jobs.Manage.ShouldBe("BackgroundJobs.Jobs.Manage");

    [Fact]
    public void Jobs_Read_StartsWithGroupName() => BackgroundJobsPermissions.Jobs.Read.ShouldStartWith(BackgroundJobsPermissions.GroupName + ".");

    [Fact]
    public void Jobs_Manage_StartsWithGroupName() => BackgroundJobsPermissions.Jobs.Manage.ShouldStartWith(BackgroundJobsPermissions.GroupName + ".");
}
