using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.StackExchangeRedis.Internal;
using Granit.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.StackExchangeRedis.Tests;

public sealed class GranitHttpIdempotencyStackExchangeRedisModuleTests
{
    private static ServiceConfigurationContext NewContext(
        HostApplicationBuilder builder, Dictionary<string, string?>? config = null)
    {
        if (config is not null)
        {
            builder.Configuration.AddInMemoryCollection(config);
        }

        return new ServiceConfigurationContext(builder.Services, builder.Configuration, builder);
    }

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        new GranitHttpIdempotencyStackExchangeRedisModule().ShouldBeAssignableTo<GranitModule>();

    [Fact]
    public void Module_DependsOnGranitHttpIdempotencyModule()
    {
        var attributes = (DependsOnAttribute[])Attribute.GetCustomAttributes(
            typeof(GranitHttpIdempotencyStackExchangeRedisModule), typeof(DependsOnAttribute));

        attributes.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitHttpIdempotencyModule));
    }

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        GranitHttpIdempotencyStackExchangeRedisModule module = new();

        module.IsEnabled(NewContext(builder)).ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_FalseWhenDisabledInConfiguration()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        GranitHttpIdempotencyStackExchangeRedisModule module = new();

        ServiceConfigurationContext context = NewContext(builder, new Dictionary<string, string?>
        {
            ["Http:Idempotency:Redis:IsEnabled"] = "false",
        });

        module.IsEnabled(context).ShouldBeFalse();
    }

    [Fact]
    public void ConfigureServices_RegistersRedisStore()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        GranitHttpIdempotencyStackExchangeRedisModule module = new();

        module.ConfigureServices(NewContext(builder));

        builder.Services.ShouldContain(d =>
            d.ServiceType == typeof(IIdempotencyStore) &&
            d.ImplementationType == typeof(RedisIdempotencyStore) &&
            d.Lifetime == ServiceLifetime.Singleton);
    }
}
