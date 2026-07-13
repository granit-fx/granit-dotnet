using Granit.Notifications.AwsSes.HealthChecks;
using Granit.Notifications.AwsSes.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AwsSes.Tests;

/// <summary>
/// Unit tests for <see cref="AwsSesHealthCheck"/>.
/// Only tests the exception path — happy-path requires a real SES endpoint.
/// </summary>
public sealed class AwsSesHealthCheckTests
{
    private static HealthCheckContext CreateContext() =>
        new() { Registration = new HealthCheckRegistration("test", _ => null!, null, null) };

    [Fact]
    public async Task CheckHealthAsync_UnreachableEndpoint_ReturnsUnhealthyWithSanitizedMessage()
    {
        // Arrange — point to a non-existent SES endpoint
        AwsSesOptions options = new()
        {
            Region = "us-east-1",
            AccessKeyId = "test-access-key",
            SecretAccessKey = "test-secret-key",
        };

        AwsSesHealthCheck sut = new(Microsoft.Extensions.Options.Options.Create(options));

        // Act
        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldStartWith("SES unreachable:");
        // Must not expose credentials or region
        result.Description!.ShouldNotContain("test-access-key");
        result.Description!.ShouldNotContain("test-secret-key");
    }
}
