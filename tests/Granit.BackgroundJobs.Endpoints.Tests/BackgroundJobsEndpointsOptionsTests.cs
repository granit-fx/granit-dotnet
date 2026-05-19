using Granit.BackgroundJobs.Endpoints.Options;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Endpoints.Tests;

public sealed class BackgroundJobsEndpointsOptionsTests
{
    [Fact]
    public void RoutePrefix_Default_ShouldBeBackgroundJobs() =>
        new BackgroundJobsEndpointsOptions().RoutePrefix.ShouldBe("background-jobs");

    [Fact]
    public void TagName_Default_ShouldBeBackgroundJobs() =>
        new BackgroundJobsEndpointsOptions().TagName.ShouldBe("Background Jobs");
}
