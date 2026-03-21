using Granit.Http.Resilience.Extensions;
using Granit.Http.Resilience.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Resilience.Tests;

public sealed class HttpResilienceServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitHttpClient_ReturnsIHttpClientBuilder()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        IHttpClientBuilder clientBuilder = builder.Services.AddGranitHttpClient("test");

        clientBuilder.ShouldNotBeNull();
        clientBuilder.Name.ShouldBe("test");
    }

    [Fact]
    public void AddGranitHttpClient_WithProviderConfigure_AppliesCallback()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        Uri expectedBaseAddress = new("https://configured.example.com");

        builder.Services.AddGranitHttpClient("configured-client",
            (_, client) => client.BaseAddress = expectedBaseAddress);

        using IHost host = builder.Build();
        IHttpClientFactory factory = host.Services.GetRequiredService<IHttpClientFactory>();
        HttpClient client = factory.CreateClient("configured-client");

        client.BaseAddress.ShouldBe(expectedBaseAddress);
    }

    [Fact]
    public void AddGranitHttpClient_WithSimpleConfigure_AppliesCallback()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        Uri expectedBaseAddress = new("https://api.example.com");

        builder.Services.AddGranitHttpClient("simple-client",
            client => client.BaseAddress = expectedBaseAddress);

        using IHost host = builder.Build();
        IHttpClientFactory factory = host.Services.GetRequiredService<IHttpClientFactory>();
        HttpClient client = factory.CreateClient("simple-client");

        client.BaseAddress.ShouldBe(expectedBaseAddress);
    }

    [Fact]
    public void AddGranitHttpClient_RegistersPostConfigureOptions()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddGranitHttpClient("my-client");

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IPostConfigureOptions<HttpStandardResilienceOptions>));
    }

    [Fact]
    public void AddGranitHttpClient_NullConfigure_RegistersWithoutCallback()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        IHttpClientBuilder clientBuilder = builder.Services.AddGranitHttpClient("no-config",
            (Action<IServiceProvider, HttpClient>?)null);

        clientBuilder.ShouldNotBeNull();

        using IHost host = builder.Build();
        IHttpClientFactory factory = host.Services.GetRequiredService<IHttpClientFactory>();
        HttpClient client = factory.CreateClient("no-config");

        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddAuthTokenPropagation_RegistersAuthTokenPropagationHandler()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Services
            .AddGranitHttpClient("auth-client")
            .AddAuthTokenPropagation();

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(AuthTokenPropagationHandler));
    }

    [Fact]
    public void AddAuthTokenPropagation_ReturnsBuilder()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        IHttpClientBuilder clientBuilder = builder.Services
            .AddGranitHttpClient("chain-client")
            .AddAuthTokenPropagation();

        clientBuilder.ShouldNotBeNull();
        clientBuilder.Name.ShouldBe("chain-client");
    }

    [Fact]
    public void AddAuthTokenPropagation_CalledTwice_DoesNotDuplicate()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.Services
            .AddGranitHttpClient("dup-client")
            .AddAuthTokenPropagation();

        builder.Services
            .AddGranitHttpClient("dup-client-2")
            .AddAuthTokenPropagation();

        int accessorCount = builder.Services.Count(d =>
            d.ServiceType == typeof(IHttpContextAccessor));

        accessorCount.ShouldBe(1);
    }

    [Fact]
    public void AddGranitHttpClient_PerClientConfig_BindsFromConfiguration()
    {
        Dictionary<string, string?> configData = new()
        {
            ["HttpResilience:my-api:Retry:MaxRetryAttempts"] = "5"
        };

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(configData);
        builder.Services.AddGranitHttpClient("my-api");

        using IHost host = builder.Build();
        IOptionsMonitor<HttpStandardResilienceOptions> monitor =
            host.Services.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();

        HttpStandardResilienceOptions options = monitor.Get("my-api-standard");

        options.Retry.MaxRetryAttempts.ShouldBe(5);
    }

    [Fact]
    public void AddGranitHttpClient_NoMatchingConfig_UsesDefaults()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddGranitHttpClient("unconfigured-client");

        using IHost host = builder.Build();
        IOptionsMonitor<HttpStandardResilienceOptions> monitor =
            host.Services.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();

        HttpStandardResilienceOptions options = monitor.Get("unconfigured-client-standard");

        options.Retry.MaxRetryAttempts.ShouldBe(3);
    }
}
