using Granit.Http.OutputCaching.Eviction;
using Granit.Http.OutputCaching.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
}
