using Granit.Identity.Federated.GoogleCloud.HealthChecks;
using Granit.Identity.Federated.GoogleCloud.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.GoogleCloud.Tests;

public sealed class GoogleCloudIdentityHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenProjectIdConfigured()
    {
        GoogleCloudIdentityOptions opts = new() { ProjectId = "my-project" };
        GoogleCloudIdentityHealthCheck sut = new(Microsoft.Extensions.Options.Options.Create(opts));

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenProjectIdMissing(string? projectId)
    {
        GoogleCloudIdentityOptions opts = new() { ProjectId = projectId! };
        GoogleCloudIdentityHealthCheck sut = new(Microsoft.Extensions.Options.Options.Create(opts));

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }
}
