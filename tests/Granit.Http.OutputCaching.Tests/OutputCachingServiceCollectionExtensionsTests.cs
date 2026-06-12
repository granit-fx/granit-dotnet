using Granit.Http.OutputCaching.Eviction;
using Granit.Http.OutputCaching.Extensions;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.Tests;

public sealed class OutputCachingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitOutputCaching_RegistersEvictionService()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        services.AddGranitOutputCaching();

        ServiceProvider provider = services.BuildServiceProvider();
        IOutputCacheEvictionService? evictionService = provider.GetService<IOutputCacheEvictionService>();
        evictionService.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitOutputCaching_DoesNotThrowOnDoubleRegistration()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        Should.NotThrow(() =>
        {
            services.AddGranitOutputCaching();
            services.AddGranitOutputCaching();
        });
    }

    [Fact]
    public void AddGranitOutputCaching_FlowsConfiguredExpirationIntoOutputCacheOptions()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Http:OutputCaching:DefaultExpiration"] = "00:05:00",
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        services.AddGranitOutputCaching();

        ServiceProvider provider = services.BuildServiceProvider();
        OutputCacheOptions outputCacheOptions =
            provider.GetRequiredService<IOptions<OutputCacheOptions>>().Value;

        // Proves the Granit options are consumed by the ASP.NET output-cache configuration
        // delegate that also wires the privacy/tenant toggles and the VaryByQuery keys.
        outputCacheOptions.DefaultExpirationTimeSpan.ShouldBe(TimeSpan.FromMinutes(5));
    }
}
