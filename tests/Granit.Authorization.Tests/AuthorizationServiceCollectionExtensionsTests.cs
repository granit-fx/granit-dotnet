// =============================================================================
// Tests - AuthorizationServiceCollectionExtensions
// =============================================================================
// Vérifie que AddGranitAuthorization enregistre tous les services RBAC
// nécessaires et retourne la collection pour le chaînage.
// =============================================================================

using Granit.Authorization.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class AuthorizationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitAuthorization_RegistersPermissionDefinitionManager()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        // Act
        services.AddGranitAuthorization();

        // Assert
        using ServiceProvider sp = services.BuildServiceProvider();
        IPermissionDefinitionManager manager = sp.GetRequiredService<IPermissionDefinitionManager>();
        manager.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitAuthorization_RegistersNullPermissionGrantStore()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddLogging();

        // Act
        services.AddGranitAuthorization();

        // Assert
        using ServiceProvider sp = services.BuildServiceProvider();
        IPermissionGrantStore store = sp.GetRequiredService<IPermissionGrantStore>();
        store.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitAuthorization_RegistersPermissionManagerReader()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitAuthorization();

        // Assert
        services.ShouldContain(sd => sd.ServiceType == typeof(IPermissionManagerReader));
    }

    [Fact]
    public void AddGranitAuthorization_RegistersPermissionManagerWriter()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitAuthorization();

        // Assert
        services.ShouldContain(sd => sd.ServiceType == typeof(IPermissionManagerWriter));
    }

    [Fact]
    public void AddGranitAuthorization_RegistersPermissionChecker()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitAuthorization();

        // Assert — descriptor registered (transitive dependencies not required here)
        services.ShouldContain(sd => sd.ServiceType == typeof(IPermissionChecker));
    }

    [Fact]
    public void AddGranitAuthorization_RegistersAuthorizationHandler()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitAuthorization();

        // Assert — descriptor registered
        services.ShouldContain(sd => sd.ServiceType == typeof(IAuthorizationHandler));
    }

    [Fact]
    public void AddGranitAuthorization_ReturnsServices_ForChaining()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection returned = services.AddGranitAuthorization();

        // Assert
        returned.ShouldBeSameAs(services);
    }

    // =========================================================================
    // DynamicPermissionPolicyProvider registration
    // =========================================================================

    [Fact]
    public void AddGranitAuthorization_RegistersDynamicPolicyProvider()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitAuthorization();

        // Assert
        services.ShouldContain(sd => sd.ServiceType == typeof(IAuthorizationPolicyProvider));
    }

    // =========================================================================
    // AuthorizationMetrics registration
    // =========================================================================

    [Fact]
    public void AddGranitAuthorization_RegistersAuthorizationMetrics()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitAuthorization();

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(Granit.Authorization.Diagnostics.AuthorizationMetrics));
    }

    // =========================================================================
    // Options validation registration
    // =========================================================================

    [Fact]
    public void AddGranitAuthorization_RegistersOptionsValidator()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitAuthorization();

        // Assert
        services.ShouldContain(sd =>
            sd.ServiceType == typeof(Microsoft.Extensions.Options.IValidateOptions<
                Granit.Authorization.Options.GranitAuthorizationOptions>));
    }

    // =========================================================================
    // TryAdd semantics — custom store not overwritten
    // =========================================================================

    [Fact]
    public void AddGranitAuthorization_CustomStoreAlreadyRegistered_DoesNotOverwrite()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton<IPermissionGrantStore>(
            NSubstitute.Substitute.For<IPermissionGrantStore>());

        // Act
        services.AddGranitAuthorization();

        // Assert — there should be exactly one IPermissionGrantStore registration
        // and it should be the custom one (added first), not NullPermissionGrantStore
        services.Count(sd => sd.ServiceType == typeof(IPermissionGrantStore)).ShouldBe(1);
    }

    // =========================================================================
    // Idempotent registration
    // =========================================================================

    [Fact]
    public void AddGranitAuthorization_CalledTwice_DoesNotDuplicateTryAddRegistrations()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitAuthorization();
        services.AddGranitAuthorization();

        // Assert — TryAdd services should appear once
        services.Count(sd => sd.ServiceType == typeof(IPermissionGrantStore)).ShouldBe(1);
        services.Count(sd =>
            sd.ServiceType == typeof(Granit.Authorization.Diagnostics.AuthorizationMetrics)).ShouldBe(1);
    }
}
