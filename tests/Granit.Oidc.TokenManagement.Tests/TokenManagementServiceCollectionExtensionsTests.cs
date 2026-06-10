using System.Diagnostics.Metrics;
using Granit.Oidc.TokenManagement.Cache;
using Granit.Oidc.TokenManagement.Extensions;
using Granit.Oidc.TokenManagement.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Oidc.TokenManagement.Tests;

public sealed class TokenManagementServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitTokenManagement_RegistersTokenEndpointService()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        services.AddGranitTokenManagement();

        using ServiceProvider provider = services.BuildServiceProvider();
        ITokenEndpointService? service = provider.GetService<ITokenEndpointService>();
        service.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitTokenManagement_RegistersTokenRevocationService()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        services.AddGranitTokenManagement();

        using ServiceProvider provider = services.BuildServiceProvider();
        ITokenRevocationService? service = provider.GetService<ITokenRevocationService>();
        service.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitTokenManagement_RegistersClientCredentialsTokenCache()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        services.AddGranitTokenManagement();

        using ServiceProvider provider = services.BuildServiceProvider();
        IClientCredentialsTokenCache? service = provider.GetService<IClientCredentialsTokenCache>();
        service.ShouldNotBeNull();
    }

    [Fact]
    public void AddClientCredentialsHttpClient_RegistersNamedClient()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        services.AddClientCredentialsHttpClient("test-api", options =>
        {
            options.Authority = "https://idp.example.com";
            options.ClientId = "my-client";
            options.ClientSecret = "my-secret";
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
        HttpClient client = httpClientFactory.CreateClient("test-api");
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitTokenManagement_IsIdempotent()
    {
        ServiceCollection services = new();
        AddRequiredDependencies(services);

        services.AddGranitTokenManagement();
        services.AddGranitTokenManagement();

        using ServiceProvider provider = services.BuildServiceProvider();
        ITokenEndpointService? service = provider.GetService<ITokenEndpointService>();
        service.ShouldNotBeNull();
    }

    private static void AddRequiredDependencies(ServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton<IFusionCache>(_ => new FusionCache(new FusionCacheOptions()));
        services.AddSingleton(Substitute.For<Granit.Oidc.Discovery.IDiscoveryDocumentService>());
        services.AddSingleton(Substitute.For<Granit.Oidc.DPoP.IDPoPProofService>());
        services.AddSingleton(Substitute.For<Granit.Timing.IClock>());
        services.AddSingleton<IMeterFactory>(new TestMeterFactory());

        // A real host always provides IHostEnvironment; the options validators depend on it.
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);
        services.AddSingleton(environment);
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in _meters)
            {
                meter.Dispose();
            }

            _meters.Clear();
        }
    }
}
