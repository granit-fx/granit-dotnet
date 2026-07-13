using Granit.Notifications.Smtp.HealthChecks;
using Granit.Notifications.Smtp.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Smtp.Tests;

/// <summary>
/// Unit tests for <see cref="SmtpHealthCheck"/>.
/// Only tests the exception path — happy-path requires a real SMTP server.
/// </summary>
public sealed class SmtpHealthCheckTests
{
    private static HealthCheckContext CreateContext() =>
        new() { Registration = new HealthCheckRegistration("test", _ => null!, null, null) };

    [Fact]
    public async Task CheckHealthAsync_UnreachableServer_ReturnsUnhealthyWithSanitizedMessage()
    {
        // Arrange — point to a non-existent SMTP server with very short timeout
        SmtpOptions options = new()
        {
            Host = "127.0.0.1",
            Port = 1, // unreachable port
            UseSsl = false,
            TimeoutSeconds = 1,
        };

        SmtpHealthCheck sut = new(Microsoft.Extensions.Options.Options.Create(options));

        // Act
        HealthCheckResult result = await sut.CheckHealthAsync(
            CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldStartWith("SMTP unreachable:");
        // Must not expose host or port
        result.Description!.ShouldNotContain("127.0.0.1");
    }
}
