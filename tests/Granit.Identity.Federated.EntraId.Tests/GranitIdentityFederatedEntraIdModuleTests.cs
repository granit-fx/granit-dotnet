using Granit.Identity.Federated.EntraId.Extensions;
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
        builder.Configuration["EntraIdAdmin:TenantId"] = "test-tenant-id";
        builder.Configuration["EntraIdAdmin:ClientId"] = "admin-service";
        builder.Configuration["EntraIdAdmin:ClientSecret"] = "secret";
        builder.Configuration["EntraIdAdmin:ServicePrincipalObjectId"] = "sp-obj-id";

        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);
        var module = new GranitIdentityFederatedEntraIdModule();

        module.ConfigureServices(context);

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdentityProvider));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(EntraIdIdentityProvider));
        descriptor.Lifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void ConfigureServices_RegistersTokenServiceAsSingleton()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["EntraIdAdmin:TenantId"] = "test-tenant-id";
        builder.Configuration["EntraIdAdmin:ClientId"] = "admin-service";
        builder.Configuration["EntraIdAdmin:ClientSecret"] = "secret";
        builder.Configuration["EntraIdAdmin:ServicePrincipalObjectId"] = "sp-obj-id";

        var context = new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);
        var module = new GranitIdentityFederatedEntraIdModule();

        module.ConfigureServices(context);

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(EntraIdAdminTokenService));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }
}
