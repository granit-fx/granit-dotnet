using Granit.Identity.Federated.GoogleCloud.HealthChecks;
using Granit.Identity.Federated.GoogleCloud.Internal;
using Granit.Identity.Federated.GoogleCloud.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.GoogleCloud.Tests;

public sealed class GoogleCloudIdentityHealthCheckTests
{
    private readonly IFirebaseAuthTransport _transport = Substitute.For<IFirebaseAuthTransport>();

    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthy_WhenPingSucceeds()
    {
        GoogleCloudIdentityOptions opts = new() { ProjectId = "my-project" };
        _transport.PingAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        GoogleCloudIdentityHealthCheck sut = new(_transport, Microsoft.Extensions.Options.Options.Create(opts));

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
        GoogleCloudIdentityHealthCheck sut = new(_transport, Microsoft.Extensions.Options.Options.Create(opts));

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        await _transport.DidNotReceive().PingAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthy_WhenGenericExceptionThrown()
    {
        GoogleCloudIdentityOptions opts = new() { ProjectId = "my-project" };
        _transport.PingAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException("network down"));
        GoogleCloudIdentityHealthCheck sut = new(_transport, Microsoft.Extensions.Options.Options.Create(opts));

        HealthCheckResult result = await sut.CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description.ShouldBe("Firebase Auth probe failed: HttpRequestException");
    }
}
