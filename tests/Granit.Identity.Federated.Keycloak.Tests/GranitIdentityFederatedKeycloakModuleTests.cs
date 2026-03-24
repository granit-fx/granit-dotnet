using Granit.Identity.Federated.Keycloak.Extensions;
using Granit.Identity.Federated.Keycloak.Internal;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Keycloak.Tests;

public sealed class GranitIdentityFederatedKeycloakModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        var module = new GranitIdentityFederatedKeycloakModule();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_HasDependsOnGranitIdentityModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitIdentityFederatedKeycloakModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();
        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitIdentityFederatedModule)));
    }

    [Fact]
    public void ConfigureServices_RegistersKeycloakIdentityProvider()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["KeycloakAdmin:BaseUrl"] = "https://keycloak.test";
        builder.Configuration["KeycloakAdmin:Realm"] = "test-realm";
        builder.Configuration["KeycloakAdmin:ClientId"] = "admin-service";
        builder.Configuration["KeycloakAdmin:ClientSecret"] = "secret";

        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);
        var module = new GranitIdentityFederatedKeycloakModule();

        module.ConfigureServices(context);

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdentityProvider));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(KeycloakIdentityProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void ConfigureServices_RegistersTokenServiceAsSingleton()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["KeycloakAdmin:BaseUrl"] = "https://keycloak.test";
        builder.Configuration["KeycloakAdmin:Realm"] = "test-realm";
        builder.Configuration["KeycloakAdmin:ClientId"] = "admin-service";
        builder.Configuration["KeycloakAdmin:ClientSecret"] = "secret";

        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);
        var module = new GranitIdentityFederatedKeycloakModule();

        module.ConfigureServices(context);

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(KeycloakAdminTokenService));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }
}
