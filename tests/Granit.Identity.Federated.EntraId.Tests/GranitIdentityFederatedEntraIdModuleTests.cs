using Granit.Identity.Federated.EntraId.Internal;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class GranitIdentityFederatedEntraIdModuleTests
{
    [Fact]
    public void Module_InheritsFromGranitModule()
    {
        var module = new GranitIdentityFederatedEntraIdModule();

        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_HasDependsOnGranitIdentityModule()
    {
        DependsOnAttribute[] attributes = typeof(GranitIdentityFederatedEntraIdModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attributes.ShouldNotBeEmpty();
        attributes.ShouldContain(a => a.DependedTypes.Contains(typeof(GranitIdentityFederatedModule)));
    }

    [Fact]
    public void ConfigureServices_RegistersEntraIdIdentityProvider()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Identity:Federated:EntraId:TenantId"] = "test-tenant-id";
        builder.Configuration["Identity:Federated:EntraId:ClientId"] = "admin-service";
        builder.Configuration["Identity:Federated:EntraId:ClientSecret"] = "secret";
        builder.Configuration["Identity:Federated:EntraId:ServicePrincipalObjectId"] = "sp-obj-id";

        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);
        var module = new GranitIdentityFederatedEntraIdModule();

        module.ConfigureServices(context);

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdentityProvider));
        // IIdentityProvider now resolves to the graceful-degradation decorator (a factory
        // registration); the concrete provider is registered by its own type.
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBeNull();
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
        ServiceDescriptor? concrete = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(EntraIdIdentityProvider));
        concrete.ShouldNotBeNull();
        concrete!.ImplementationType.ShouldBe(typeof(EntraIdIdentityProvider));
    }

    [Fact]
    public void ConfigureServices_RegistersTokenServiceAsSingleton()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Identity:Federated:EntraId:TenantId"] = "test-tenant-id";
        builder.Configuration["Identity:Federated:EntraId:ClientId"] = "admin-service";
        builder.Configuration["Identity:Federated:EntraId:ClientSecret"] = "secret";
        builder.Configuration["Identity:Federated:EntraId:ServicePrincipalObjectId"] = "sp-obj-id";

        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);
        var module = new GranitIdentityFederatedEntraIdModule();

        module.ConfigureServices(context);

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(EntraIdAdminTokenService));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }
}
