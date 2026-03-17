using Granit.Http.OutputCaching.Eviction;
using Granit.Http.OutputCaching.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.Tests;

public sealed class GranitOutputCachingModuleTests
{
    private static ServiceProvider BuildServiceProvider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddGranitOutputCaching();
        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void AddGranitOutputCaching_RegistersEvictionService()
    {
        using ServiceProvider sp = BuildServiceProvider();

        sp.GetService<IOutputCacheEvictionService>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitOutputCaching_RegistersOutputCachingOptions()
    {
        using ServiceProvider sp = BuildServiceProvider();
        Options.OutputCachingOptions options =
            sp.GetRequiredService<IOptions<Options.OutputCachingOptions>>().Value;

        options.DefaultExpiration.ShouldBe(TimeSpan.FromSeconds(60));
        options.EnableTenantIsolation.ShouldBeTrue();
        options.ExcludeAuthenticatedResponses.ShouldBeTrue();
        options.VaryByQueryKeys.ShouldContain("page");
        options.VaryByQueryKeys.ShouldContain("include");
        options.VaryByQueryKeys.ShouldContain("expand");
    }
}
