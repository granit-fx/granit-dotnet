using Granit.Vault.HashiCorp.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using VaultSharp;
using VaultSharp.V1.SystemBackend;
using Xunit;
using MsHealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;
using VaultSystemHealth = VaultSharp.V1.SystemBackend.HealthStatus;

namespace Granit.Vault.HashiCorp.Tests;

public sealed class VaultHealthCheckTests
{
    private static HealthCheckContext BuildContext() =>
        new() { Registration = new HealthCheckRegistration("vault", _ => Substitute.For<IHealthCheck>(), null, []) };

    [Fact]
    public async Task CheckHealthAsync_ActiveVault_ReturnsHealthy()
    {
        IVaultClient vaultClient = Substitute.For<IVaultClient>();
        VaultSystemHealth status = new() { Sealed = false, Standby = false };
        vaultClient.V1.System.GetHealthStatusAsync().Returns(status);

        VaultHealthCheck sut = new(vaultClient);

        HealthCheckResult result = await sut.CheckHealthAsync(BuildContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(MsHealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_StandbyVault_ReturnsDegraded()
    {
        IVaultClient vaultClient = Substitute.For<IVaultClient>();
        VaultSystemHealth status = new() { Sealed = false, Standby = true };
        vaultClient.V1.System.GetHealthStatusAsync().Returns(status);

        VaultHealthCheck sut = new(vaultClient);

        HealthCheckResult result = await sut.CheckHealthAsync(BuildContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(MsHealthStatus.Degraded);
        result.Description.ShouldBe("Vault standby");
    }

    [Fact]
    public async Task CheckHealthAsync_SealedVault_ReturnsUnhealthy()
    {
        IVaultClient vaultClient = Substitute.For<IVaultClient>();
        VaultSystemHealth status = new() { Sealed = true, Standby = false };
        vaultClient.V1.System.GetHealthStatusAsync().Returns(status);

        VaultHealthCheck sut = new(vaultClient);

        HealthCheckResult result = await sut.CheckHealthAsync(BuildContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(MsHealthStatus.Unhealthy);
        result.Description.ShouldBe("Vault sealed");
    }

    [Fact]
    public async Task CheckHealthAsync_ExceptionThrown_ReturnsUnhealthyWithSanitizedMessage()
    {
        IVaultClient vaultClient = Substitute.For<IVaultClient>();
        vaultClient.V1.System.GetHealthStatusAsync().Throws(new HttpRequestException("Connection refused to https://vault:8200 with token=s.secret"));

        VaultHealthCheck sut = new(vaultClient);

        HealthCheckResult result = await sut.CheckHealthAsync(BuildContext(), TestContext.Current.CancellationToken);

        result.Status.ShouldBe(MsHealthStatus.Unhealthy);
        result.Description.ShouldBe("Vault unreachable: HttpRequestException");
        result.Description!.ShouldNotContain("vault:8200");
        result.Description!.ShouldNotContain("token");
    }
}
