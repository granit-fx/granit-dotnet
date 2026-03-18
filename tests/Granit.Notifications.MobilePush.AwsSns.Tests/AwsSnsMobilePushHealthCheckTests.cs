using Granit.Notifications.MobilePush.AwsSns.HealthChecks;
using Granit.Notifications.MobilePush.AwsSns.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.MobilePush.AwsSns.Tests;

public sealed class AwsSnsMobilePushHealthCheckTests
{
    private static HealthCheckContext CreateContext() =>
        new() { Registration = new HealthCheckRegistration("test", _ => null!, null, null) };

    [Fact]
    public async Task CheckHealthAsync_RegionAndArnConfigured_ReturnsHealthy()
    {
        AwsSnsMobilePushOptions options = new()
        {
            Region = "eu-west-1",
            PlatformApplicationArn = "arn:aws:sns:eu-west-1:123:app/GCM/MyApp",
        };

        AwsSnsMobilePushHealthCheck sut = new(Microsoft.Extensions.Options.Options.Create(options));

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_RegionEmpty_ReturnsUnhealthy()
    {
        AwsSnsMobilePushOptions options = new()
        {
            Region = string.Empty,
            PlatformApplicationArn = "arn:aws:sns:eu-west-1:123:app/GCM/MyApp",
        };

        AwsSnsMobilePushHealthCheck sut = new(Microsoft.Extensions.Options.Options.Create(options));

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("region");
    }

    [Fact]
    public async Task CheckHealthAsync_ArnEmpty_ReturnsUnhealthy()
    {
        AwsSnsMobilePushOptions options = new()
        {
            Region = "eu-west-1",
            PlatformApplicationArn = string.Empty,
        };

        AwsSnsMobilePushHealthCheck sut = new(Microsoft.Extensions.Options.Options.Create(options));

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("PlatformApplicationArn");
    }

    [Fact]
    public async Task CheckHealthAsync_OptionsThrows_ReturnsUnhealthy()
    {
        AwsSnsMobilePushHealthCheck sut = new(new ThrowingOptions());

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldStartWith("SNS mobile push health check failed:");
    }

    private sealed class ThrowingOptions : IOptions<AwsSnsMobilePushOptions>
    {
        public AwsSnsMobilePushOptions Value => throw new InvalidOperationException("Configuration unavailable");
    }
}
