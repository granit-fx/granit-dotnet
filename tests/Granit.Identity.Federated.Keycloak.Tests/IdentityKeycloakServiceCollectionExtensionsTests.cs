using Granit.Identity.Federated.Keycloak.Extensions;
using Granit.Identity.Federated.Keycloak.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests;

public sealed class IdentityKeycloakServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitIdentityKeycloak_RegistersIdentityProvider()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["KeycloakAdmin:BaseUrl"] = "https://keycloak.test";
        builder.Configuration["KeycloakAdmin:Realm"] = "test-realm";
        builder.Configuration["KeycloakAdmin:ClientId"] = "admin-service";
        builder.Configuration["KeycloakAdmin:ClientSecret"] = "secret";

        builder.Services.AddGranitIdentityKeycloak();

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdentityProvider));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(KeycloakIdentityProvider));
    }

    [Fact]
    public void AddGranitIdentityKeycloak_RegistersTokenService()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["KeycloakAdmin:BaseUrl"] = "https://keycloak.test";
        builder.Configuration["KeycloakAdmin:Realm"] = "test-realm";
        builder.Configuration["KeycloakAdmin:ClientId"] = "admin-service";
        builder.Configuration["KeycloakAdmin:ClientSecret"] = "secret";

        builder.Services.AddGranitIdentityKeycloak();

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(KeycloakAdminTokenService));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitIdentityKeycloak_RegistersHttpClient()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["KeycloakAdmin:BaseUrl"] = "https://keycloak.test";
        builder.Configuration["KeycloakAdmin:Realm"] = "test-realm";
        builder.Configuration["KeycloakAdmin:ClientId"] = "admin-service";
        builder.Configuration["KeycloakAdmin:ClientSecret"] = "secret";

        builder.Services.AddGranitIdentityKeycloak();

        // IHttpClientFactory should be registered after AddHttpClient.
        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(IHttpClientFactory));
    }

    [Fact]
    public void AddGranitIdentityKeycloak_ReturnsSameServiceCollection()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["KeycloakAdmin:BaseUrl"] = "https://keycloak.test";
        builder.Configuration["KeycloakAdmin:Realm"] = "test-realm";
        builder.Configuration["KeycloakAdmin:ClientId"] = "admin-service";
        builder.Configuration["KeycloakAdmin:ClientSecret"] = "secret";

        IServiceCollection result = builder.Services.AddGranitIdentityKeycloak();

        result.ShouldBeSameAs(builder.Services);
    }

    [Fact]
    public void AddGranitIdentityKeycloak_ConfiguresOptionsFromConfiguration()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["KeycloakAdmin:BaseUrl"] = "https://keycloak.test";
        builder.Configuration["KeycloakAdmin:Realm"] = "my-realm";
        builder.Configuration["KeycloakAdmin:ClientId"] = "my-client";
        builder.Configuration["KeycloakAdmin:ClientSecret"] = "my-secret";

        builder.Services.AddGranitIdentityKeycloak();

        // Options configuration should be registered.
        builder.Services.ShouldContain(
            d => d.ServiceType.FullName != null
                 && d.ServiceType.FullName.Contains("IConfigureOptions"));
    }
}
