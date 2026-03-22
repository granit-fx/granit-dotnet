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
        builder.Configuration["EntraIdAdmin:TenantId"] = "test-tenant-id";
        builder.Configuration["EntraIdAdmin:ClientId"] = "admin-service";
        builder.Configuration["EntraIdAdmin:ClientSecret"] = "secret";
        builder.Configuration["EntraIdAdmin:ServicePrincipalObjectId"] = "sp-obj-id";

        builder.Services.AddGranitIdentityEntraId();

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IIdentityProvider));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(EntraIdIdentityProvider));
    }

    [Fact]
    public void AddGranitIdentityEntraId_RegistersHttpClient()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["EntraIdAdmin:TenantId"] = "test-tenant-id";
        builder.Configuration["EntraIdAdmin:ClientId"] = "admin-service";
        builder.Configuration["EntraIdAdmin:ClientSecret"] = "secret";
        builder.Configuration["EntraIdAdmin:ServicePrincipalObjectId"] = "sp-obj-id";

        builder.Services.AddGranitIdentityEntraId();

        builder.Services.ShouldContain(
            d => d.ServiceType == typeof(IHttpClientFactory));
    }

    [Fact]
    public void AddGranitIdentityEntraId_RegistersTokenServiceAsSingleton()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["EntraIdAdmin:TenantId"] = "test-tenant-id";
        builder.Configuration["EntraIdAdmin:ClientId"] = "admin-service";
        builder.Configuration["EntraIdAdmin:ClientSecret"] = "secret";
        builder.Configuration["EntraIdAdmin:ServicePrincipalObjectId"] = "sp-obj-id";

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
        builder.Configuration["EntraIdAdmin:TenantId"] = "test-tenant-id";
        builder.Configuration["EntraIdAdmin:ClientId"] = "admin-service";
        builder.Configuration["EntraIdAdmin:ClientSecret"] = "secret";
        builder.Configuration["EntraIdAdmin:ServicePrincipalObjectId"] = "sp-obj-id";

        IServiceCollection result = builder.Services.AddGranitIdentityEntraId();

        result.ShouldBeSameAs(builder.Services);
    }

    [Fact]
    public void AddGranitIdentityEntraId_ConfiguresOptionsFromConfiguration()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Configuration["EntraIdAdmin:TenantId"] = "my-tenant";
        builder.Configuration["EntraIdAdmin:ClientId"] = "my-client";
        builder.Configuration["EntraIdAdmin:ClientSecret"] = "my-secret";
        builder.Configuration["EntraIdAdmin:ServicePrincipalObjectId"] = "my-sp";

        builder.Services.AddGranitIdentityEntraId();

        builder.Services.ShouldContain(
            d => d.ServiceType.FullName != null
                 && d.ServiceType.FullName.Contains("IConfigureOptions"));
    }
}
