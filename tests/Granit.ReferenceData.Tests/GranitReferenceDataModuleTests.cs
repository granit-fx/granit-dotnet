using Granit.Extensions;
using Granit.Modularity;
using Granit.ReferenceData.Extensions;
using Granit.ReferenceData.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests;

public sealed class GranitReferenceDataModuleTests
{
    private static WebApplication BuildApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddGranit<GranitReferenceDataModule>();
        return builder.Build();
    }

    [Fact]
    public void Module_Is_Discovered_In_Topological_Order()
    {
        using WebApplication app = BuildApp();

        GranitApplication granitApp = app.Services.GetRequiredService<GranitApplication>();

        granitApp.GetModuleTypes().ShouldContain(typeof(GranitReferenceDataModule));
    }

    [Fact]
    public void ReferenceDataOptions_Is_Resolvable()
    {
        using WebApplication app = BuildApp();

        IOptions<ReferenceDataOptions> options =
            app.Services.GetRequiredService<IOptions<ReferenceDataOptions>>();

        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void Default_CacheTimeToLive_Is_OneHour()
    {
        using WebApplication app = BuildApp();

        ReferenceDataOptions options =
            app.Services.GetRequiredService<IOptions<ReferenceDataOptions>>().Value;

        options.CacheTimeToLive.ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void MemoryCache_Is_Registered()
    {
        using WebApplication app = BuildApp();

        IMemoryCache cache = app.Services.GetRequiredService<IMemoryCache>();

        cache.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitReferenceData_With_Configure_Overrides_Options()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddGranitReferenceData(options =>
        {
            options.CacheTimeToLive = TimeSpan.FromMinutes(5);
        });
        using WebApplication app = builder.Build();

        ReferenceDataOptions options =
            app.Services.GetRequiredService<IOptions<ReferenceDataOptions>>().Value;

        options.CacheTimeToLive.ShouldBe(TimeSpan.FromMinutes(5));
    }
}
