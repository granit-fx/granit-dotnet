using Granit.Notifications.AwsSns.Sms.HealthChecks;
using Granit.Notifications.AwsSns.Sms.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSns.Sms.Tests;

public sealed class AwsSnsSmsHealthCheckTests
{
    private static HealthCheckContext CreateContext() =>
        new() { Registration = new HealthCheckRegistration("test", _ => null!, null, null) };

    [Fact]
    public async Task CheckHealthAsync_RegionConfigured_ReturnsHealthy()
    {
        AwsSnsSmsOptions options = new() { Region = "eu-west-1" };
        AwsSnsSmsHealthCheck sut = new(Microsoft.Extensions.Options.Options.Create(options));

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_RegionEmpty_ReturnsUnhealthy()
    {
        AwsSnsSmsOptions options = new() { Region = string.Empty };
        AwsSnsSmsHealthCheck sut = new(Microsoft.Extensions.Options.Options.Create(options));

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("region");
    }

    [Fact]
    public async Task CheckHealthAsync_RegionWhitespace_ReturnsUnhealthy()
    {
        AwsSnsSmsOptions options = new() { Region = "   " };
        AwsSnsSmsHealthCheck sut = new(Microsoft.Extensions.Options.Options.Create(options));

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task CheckHealthAsync_OptionsThrows_ReturnsUnhealthy()
    {
        IOptions<AwsSnsSmsOptions> throwingOptions = new ThrowingOptions();
        AwsSnsSmsHealthCheck sut = new(throwingOptions);

        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldStartWith("SNS SMS health check failed:");
    }

    private sealed class ThrowingOptions : IOptions<AwsSnsSmsOptions>
    {
        public AwsSnsSmsOptions Value => throw new InvalidOperationException("Configuration unavailable");
    }
}
