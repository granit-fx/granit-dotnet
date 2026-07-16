using Granit.Identity.Federated.EntraId.Extensions;
using Granit.Identity.Federated.EntraId.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class IdentityEntraIdServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitIdentityEntraId_RegistersIdentityProvider()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Identity:Federated:EntraId:TenantId"] = "test-tenant-id";
        builder.Configuration["Identity:Federated:EntraId:ClientId"] = "admin-service";
        builder.Configuration["Identity:Federated:EntraId:ClientSecret"] = "secret";
        builder.Configuration["Identity:Federated:EntraId:ServicePrincipalObjectId"] = "sp-obj-id";

        builder.Services.AddGranitIdentityEntraId();

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdentityProvider));
        // IIdentityProvider now resolves to the graceful-degradation decorator (a factory
        // registration); the concrete provider is registered by its own type.
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBeNull();
        ServiceDescriptor? concrete = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(EntraIdIdentityProvider));
        concrete.ShouldNotBeNull();
        concrete!.ImplementationType.ShouldBe(typeof(EntraIdIdentityProvider));
    }

    [Fact]
    public void AddGranitIdentityEntraId_RegistersHttpClient()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Identity:Federated:EntraId:TenantId"] = "test-tenant-id";
        builder.Configuration["Identity:Federated:EntraId:ClientId"] = "admin-service";
        builder.Configuration["Identity:Federated:EntraId:ClientSecret"] = "secret";
        builder.Configuration["Identity:Federated:EntraId:ServicePrincipalObjectId"] = "sp-obj-id";

        builder.Services.AddGranitIdentityEntraId();

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(IHttpClientFactory));
    }

    [Fact]
    public void AddGranitIdentityEntraId_RegistersTokenServiceAsSingleton()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Identity:Federated:EntraId:TenantId"] = "test-tenant-id";
        builder.Configuration["Identity:Federated:EntraId:ClientId"] = "admin-service";
        builder.Configuration["Identity:Federated:EntraId:ClientSecret"] = "secret";
        builder.Configuration["Identity:Federated:EntraId:ServicePrincipalObjectId"] = "sp-obj-id";

        builder.Services.AddGranitIdentityEntraId();

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(EntraIdAdminTokenService));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitIdentityEntraId_ReturnsSameServiceCollection()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Identity:Federated:EntraId:TenantId"] = "test-tenant-id";
        builder.Configuration["Identity:Federated:EntraId:ClientId"] = "admin-service";
        builder.Configuration["Identity:Federated:EntraId:ClientSecret"] = "secret";
        builder.Configuration["Identity:Federated:EntraId:ServicePrincipalObjectId"] = "sp-obj-id";

        IServiceCollection result = builder.Services.AddGranitIdentityEntraId();

        result.ShouldBeSameAs(builder.Services);
    }

    [Fact]
    public void AddGranitIdentityEntraId_ConfiguresOptionsFromConfiguration()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["Identity:Federated:EntraId:TenantId"] = "my-tenant";
        builder.Configuration["Identity:Federated:EntraId:ClientId"] = "my-client";
        builder.Configuration["Identity:Federated:EntraId:ClientSecret"] = "my-secret";
        builder.Configuration["Identity:Federated:EntraId:ServicePrincipalObjectId"] = "my-sp";

        builder.Services.AddGranitIdentityEntraId();

        builder.Services.ShouldContain(
            d => d.ServiceType.FullName != null
                 && d.ServiceType.FullName.Contains("IConfigureOptions"));
    }
}
