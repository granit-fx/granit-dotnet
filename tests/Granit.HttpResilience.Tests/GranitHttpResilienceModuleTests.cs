// =============================================================================
// Tests - GranitHttpResilienceModule
// =============================================================================
// Verifies module inheritance, that AddGranitHttpClient() registers services
// without throwing, and that IHttpClientFactory is resolvable.
// =============================================================================

using Granit.Core.Modularity;
using Granit.HttpResilience.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.HttpResilience.Tests;

public sealed class GranitHttpResilienceModuleTests
{
    [Fact]
    public void GranitHttpResilienceModule_IsGranitModule() =>
        typeof(GranitHttpResilienceModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitHttpResilienceModule_IsSealed() =>
        typeof(GranitHttpResilienceModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void GranitHttpResilienceModule_HasNoDependsOn()
    {
        var attributes = (DependsOnAttribute[])
            typeof(GranitHttpResilienceModule).GetCustomAttributes(typeof(DependsOnAttribute), inherit: false);

        attributes.ShouldBeEmpty();
    }

    [Fact]
    public void AddGranitHttpClient_DoesNotThrow()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        Should.NotThrow(() => builder.Services.AddGranitHttpClient("test-client"));
    }

    [Fact]
    public void AddGranitHttpClient_WithConfigureCallback_DoesNotThrow()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        Should.NotThrow(() => builder.Services.AddGranitHttpClient("test-client",
            (_, client) => client.Timeout = TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void AddGranitHttpClient_WithSimpleCallback_DoesNotThrow()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        Should.NotThrow(() => builder.Services.AddGranitHttpClient("test-client",
            client => client.Timeout = TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void AddGranitHttpClient_RegistersIHttpClientFactory()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddGranitHttpClient("test-client");

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IHttpClientFactory));
    }

    [Fact]
    public void AddGranitHttpClient_MultipleClients_DoesNotThrow()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        Should.NotThrow(() =>
        {
            builder.Services.AddGranitHttpClient("client-one");
            builder.Services.AddGranitHttpClient("client-two");
            builder.Services.AddGranitHttpClient("client-three");
        });
    }

    [Fact]
    public void AddGranitHttpClient_BuildsServiceProvider_WithoutErrors()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddGranitHttpClient("granit-webhooks");

        using IHost host = builder.Build();

        IHttpClientFactory factory = host.Services.GetRequiredService<IHttpClientFactory>();
        factory.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitHttpClient_CreatesNamedClient()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddGranitHttpClient("granit-test");

        using IHost host = builder.Build();

        IHttpClientFactory factory = host.Services.GetRequiredService<IHttpClientFactory>();
        HttpClient client = factory.CreateClient("granit-test");

        client.ShouldNotBeNull();
    }
}
