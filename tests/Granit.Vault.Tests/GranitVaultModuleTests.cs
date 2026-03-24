using Granit.Modularity;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Vault.Tests;

public sealed class GranitVaultModuleTests
{
    [Fact]
    public void IsEnabled_AlwaysReturnsTrue_BecauseAbstractionModuleHasNoEnvironmentDependency()
    {
        GranitVaultModule module = new();
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(["--environment", "Development"]);
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);

        // Abstraction module is always enabled — provider modules control their own IsEnabled
        module.IsEnabled(context).ShouldBeTrue();
    }

    [Fact]
    public void IDatabaseCredentialProvider_InterfaceExists() =>
        typeof(IDatabaseCredentialProvider).IsInterface.ShouldBeTrue();

    [Fact]
    public void ITransitEncryptionService_InterfaceExists() =>
        typeof(ITransitEncryptionService).IsInterface.ShouldBeTrue();
}
