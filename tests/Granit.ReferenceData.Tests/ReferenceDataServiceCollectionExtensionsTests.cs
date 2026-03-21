using Granit.ReferenceData.Extensions;
using Granit.ReferenceData.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests;

public sealed class ReferenceDataServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitReferenceData_RegistersOptionsDescriptor()
    {
        ServiceCollection services = new();
        services.AddGranitReferenceData();

        services.ShouldContain(d => d.ServiceType == typeof(IConfigureOptions<ReferenceDataOptions>));
    }

    [Fact]
    public void AddGranitReferenceData_RegistersMemoryCacheDescriptor()
    {
        ServiceCollection services = new();
        services.AddGranitReferenceData();

        services.ShouldContain(d => d.ServiceType == typeof(IMemoryCache));
    }

    [Fact]
    public void AddGranitReferenceData_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitReferenceData(configure: null);

        result.ShouldBe(services);
    }

    [Fact]
    public void AddGranitReferenceData_WithConfigure_RegistersPostConfigureDescriptor()
    {
        ServiceCollection services = new();
        services.AddGranitReferenceData(opts =>
        {
            opts.CacheTimeToLive = TimeSpan.FromMinutes(15);
        });

        services.ShouldContain(d =>
            d.ServiceType == typeof(IConfigureOptions<ReferenceDataOptions>));
    }

    [Fact]
    public void AddGranitReferenceData_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitReferenceData();

        result.ShouldBeSameAs(services);
    }
}
