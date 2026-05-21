using Granit.Identity.Federated.Keycloak.Extensions;
using Granit.Identity.Federated.Keycloak.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests;

public sealed class KeycloakIdentityProviderCapabilitiesTests
{
    private readonly KeycloakIdentityProviderCapabilities _capabilities = new();

    [Fact]
    public void ProviderName_ReturnsKeycloak() =>
        _capabilities.ProviderName.ShouldBe("Keycloak");

    [Fact]
    public void SupportsIndividualSessionTermination_ReturnsTrue() =>
        _capabilities.SupportsIndividualSessionTermination.ShouldBeTrue();

    [Fact]
    public void SupportsNativePasswordResetEmail_ReturnsTrue() =>
        _capabilities.SupportsNativePasswordResetEmail.ShouldBeTrue();

    [Fact]
    public void SupportsGroupHierarchy_ReturnsTrue() =>
        _capabilities.SupportsGroupHierarchy.ShouldBeTrue();

    [Fact]
    public void SupportsCustomAttributes_ReturnsTrue() =>
        _capabilities.SupportsCustomAttributes.ShouldBeTrue();

    [Fact]
    public void MaxCustomAttributes_ReturnsMaxValue() =>
        _capabilities.MaxCustomAttributes.ShouldBe(int.MaxValue);

    [Fact]
    public void SupportsCredentialVerification_ReturnsTrue() =>
        _capabilities.SupportsCredentialVerification.ShouldBeTrue();

    [Fact]
    public void SupportsUserCreation_ReturnsTrue() =>
        _capabilities.SupportsUserCreation.ShouldBeTrue();

    [Fact]
    public void SupportsGroupManagement_ReturnsFalse() =>
        _capabilities.SupportsGroupManagement.ShouldBeFalse();

    [Fact]
    public void ImplementsIIdentityProviderCapabilities() =>
        _capabilities.ShouldBeAssignableTo<IIdentityProviderCapabilities>();

    [Fact]
    public void AddGranitIdentityKeycloak_RegistersKeycloakCapabilities()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Identity:Federated:Keycloak:BaseUrl"] = "https://keycloak.test";
        builder.Configuration["Identity:Federated:Keycloak:Realm"] = "test-realm";
        builder.Configuration["Identity:Federated:Keycloak:ClientId"] = "admin-service";
        builder.Configuration["Identity:Federated:Keycloak:ClientSecret"] = "secret";

        builder.Services.AddGranitIdentityKeycloak();

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdentityProviderCapabilities));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(KeycloakIdentityProviderCapabilities));
    }
}
