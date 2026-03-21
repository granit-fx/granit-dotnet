using Granit.BackgroundJobs.Endpoints.Internal;
using Granit.BackgroundJobs.Endpoints.Permissions;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Endpoints.Tests;

public sealed class BackgroundJobsAuthorizationPolicyTests
{
    [Fact]
    public void PolicyName_EqualsManagePermission()
    {
        BackgroundJobsAuthorizationPolicy.PolicyName
            .ShouldBe(BackgroundJobsPermissions.Jobs.Manage);
    }

    [Fact]
    public void PolicyName_HasExpectedValue()
    {
        BackgroundJobsAuthorizationPolicy.PolicyName
            .ShouldBe("BackgroundJobs.Jobs.Manage");
    }
}
