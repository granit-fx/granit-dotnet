using Granit.Vault.Exceptions;
using Granit.Vault.HealthChecks;
using Granit.Vault.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Vault.Tests.HealthChecks;

public sealed class SecretStoreHealthCheckTests
{
    private readonly ISecretStore _store = Substitute.For<ISecretStore>();

    [Fact]
    public async Task CheckHealthAsync_WhenCanaryNotConfigured_ReportsHealthy()
    {
        SecretStoreHealthCheck sut = Build(canary: null);

        HealthCheckResult result = await sut.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Description!.ShouldContain("not configured");
        await _store.DidNotReceive().GetSecretAsync(Arg.Any<SecretRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCanaryRetrieved_ReportsHealthy()
    {
        _store.GetSecretAsync(Arg.Any<SecretRequest>(), Arg.Any<CancellationToken>())
            .Returns(SecretDescriptor.FromString("healthcheck/probe", "alive"));

        SecretStoreHealthCheck sut = Build(canary: "healthcheck/probe");

        HealthCheckResult result = await sut.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenCanaryNotFound_ReportsUnhealthy()
    {
        _store.GetSecretAsync(Arg.Any<SecretRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SecretNotFoundException("healthcheck/probe"));

        SecretStoreHealthCheck sut = Build(canary: "healthcheck/probe");

        HealthCheckResult result = await sut.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("not found");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAccessDenied_ReportsUnhealthy()
    {
        _store.GetSecretAsync(Arg.Any<SecretRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SecretAccessDeniedException("healthcheck/probe"));

        SecretStoreHealthCheck sut = Build(canary: "healthcheck/probe");

        HealthCheckResult result = await sut.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("access denied", Case.Insensitive);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTransient_ReportsDegraded()
    {
        _store.GetSecretAsync(Arg.Any<SecretRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new SecretVaultTransientException("healthcheck/probe", "503"));

        SecretStoreHealthCheck sut = Build(canary: "healthcheck/probe");

        HealthCheckResult result = await sut.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenConfigError_ReportsUnhealthyWithException()
    {
        var cause = new SecretVaultConfigurationException("Vault:Test", "bad config");
        _store.GetSecretAsync(Arg.Any<SecretRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(cause);

        SecretStoreHealthCheck sut = Build(canary: "healthcheck/probe");

        HealthCheckResult result = await sut.CheckHealthAsync(
            new HealthCheckContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Exception.ShouldBe(cause);
    }

    private SecretStoreHealthCheck Build(string? canary)
    {
        IOptions<SecretStoreOptions> options = Microsoft.Extensions.Options.Options.Create(
            new SecretStoreOptions { HealthCheckSecretName = canary });
        return new SecretStoreHealthCheck(_store, options);
    }
}
