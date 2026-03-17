using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Granit.Http.OutputCaching.StackExchangeRedis.Tests;

public sealed class RedisOutputCacheHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenLatencyBelowThreshold_ReturnsHealthy()
    {
        // Arrange
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        IDatabase database = Substitute.For<IDatabase>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        database.PingAsync(Arg.Any<CommandFlags>()).Returns(TimeSpan.FromMilliseconds(5));

        var sut = new HealthChecks.RedisOutputCacheHealthCheck(
            connection, TimeSpan.FromMilliseconds(100));

        // Act
        HealthCheckResult result = await sut.CheckHealthAsync(
            new HealthCheckContext { Registration = new HealthCheckRegistration("test", sut, null, null) },
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenLatencyAboveThreshold_ReturnsDegraded()
    {
        // Arrange
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        IDatabase database = Substitute.For<IDatabase>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        database.PingAsync(Arg.Any<CommandFlags>()).Returns(TimeSpan.FromMilliseconds(200));

        var sut = new HealthChecks.RedisOutputCacheHealthCheck(
            connection, TimeSpan.FromMilliseconds(100));

        // Act
        HealthCheckResult result = await sut.CheckHealthAsync(
            new HealthCheckContext { Registration = new HealthCheckRegistration("test", sut, null, null) },
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConnectionFails_ReturnsUnhealthy()
    {
        // Arrange
        IConnectionMultiplexer connection = Substitute.For<IConnectionMultiplexer>();
        IDatabase database = Substitute.For<IDatabase>();
        connection.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(database);
        database.PingAsync(Arg.Any<CommandFlags>()).Returns<TimeSpan>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "test"));

        var sut = new HealthChecks.RedisOutputCacheHealthCheck(
            connection, TimeSpan.FromMilliseconds(100));

        // Act
        HealthCheckResult result = await sut.CheckHealthAsync(
            new HealthCheckContext { Registration = new HealthCheckRegistration("test", sut, null, null) },
            TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldNotContain("localhost");
    }
}
