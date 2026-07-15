// =============================================================================
// Tests — AddGranitDataSeeding
// =============================================================================
// Vérifie que l'extension d'enregistrement DI :
//   - Enregistre IDataSeeder comme Singleton
//   - Enregistre le DataSeedingHostedService comme IHostedService
//   - N'enregistre PAS de IDataSeedContributor (responsabilité des modules)
// =============================================================================

using Granit.Persistence.DataSeeding;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.DataSeeding;

public sealed class DataSeedingRegistrationTests
{
    [Fact]
    public void AddGranitDataSeeding_RegistersDataSeeder_AsSingleton()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDataSeeding();

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDataSeeder));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
        descriptor.ImplementationType.ShouldBe(typeof(DataSeeder));
    }

    [Fact]
    public void AddGranitDataSeeding_RegistersHostedService()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDataSeeding();

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IHostedService)
                 && d.ImplementationType == typeof(DataSeedingHostedService));
        descriptor.ShouldNotBeNull();
    }

#pragma warning disable CS0618 // Obsolete: testing that legacy interface is not auto-registered
    [Fact]
    public void AddGranitDataSeeding_DoesNotRegisterContributors()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDataSeeding();

        // Assert
        services.Where(d => d.ServiceType == typeof(IDataSeedContributor))
            .ShouldBeEmpty();
    }
#pragma warning restore CS0618

    [Fact]
    public void AddGranitDataSeeding_DoesNotRegisterHostContributors()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDataSeeding();

        // Assert
        services.Where(d => d.ServiceType == typeof(IHostDataSeedContributor))
            .ShouldBeEmpty();
    }

    [Fact]
    public void AddGranitDataSeeding_DoesNotRegisterTenantContributors()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitDataSeeding();

        // Assert
        services.Where(d => d.ServiceType == typeof(ITenantDataSeedContributor))
            .ShouldBeEmpty();
    }

    [Fact]
    public void AddGranitDataSeeding_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection result = services.AddGranitDataSeeding();

        // Assert
        result.ShouldBeSameAs(services);
    }
}
