using Granit.Http.Resilience.Extensions;
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
    public void AddGranitHttpClient_RegistersConfigurationBinding()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddGranitHttpClient("my-client");

        // BindConfiguration registers an IConfigureOptions<HttpStandardResilienceOptions>
        // for the "my-client-standard" named options key.
        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<HttpStandardResilienceOptions>));
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

    // AddAuthTokenPropagation tests were removed when the handler was — the handler
    // it registered was a confused-deputy vulnerability. Its replacement lives
    // in Granit.Oidc.TokenManagement (AddOnBehalfOfHttpClient) and has its own
    // test suite there.

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
