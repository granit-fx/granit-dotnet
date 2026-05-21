using Granit.Modularity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.StackExchangeRedis.Tests;

public sealed class GranitHttpOutputCachingStackExchangeRedisModuleIsEnabledTests
{
    [Fact]
    public void IsEnabled_WhenIsEnabledTrue_ReturnsTrue()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Http:OutputCaching:Redis:IsEnabled"] = "true",
            })
            .Build();

        GranitHttpOutputCachingStackExchangeRedisModule module = new();
        ServiceConfigurationContext context = CreateContext(configuration);

        bool result = module.IsEnabled(context);

        result.ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_WhenIsEnabledFalse_ReturnsFalse()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Http:OutputCaching:Redis:IsEnabled"] = "false",
            })
            .Build();

        GranitHttpOutputCachingStackExchangeRedisModule module = new();
        ServiceConfigurationContext context = CreateContext(configuration);

        bool result = module.IsEnabled(context);

        result.ShouldBeFalse();
    }

    [Fact]
    public void IsEnabled_WhenSectionMissing_ReturnsTrue()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        GranitHttpOutputCachingStackExchangeRedisModule module = new();
        ServiceConfigurationContext context = CreateContext(configuration);

        bool result = module.IsEnabled(context);

        result.ShouldBeTrue();
    }

    [Fact]
    public void Module_DependsOnOutputCachingModule()
    {
        var attribute = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitHttpOutputCachingStackExchangeRedisModule), typeof(DependsOnAttribute));

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitHttpOutputCachingModule));
    }

    private static ServiceConfigurationContext CreateContext(IConfiguration configuration)
    {
        ServiceCollection services = new();
        IHostApplicationBuilder builder = Substitute.For<IHostApplicationBuilder>();
        builder.Services.Returns(services);
        builder.Configuration.Returns((IConfigurationManager)new ConfigurationManager());

        return new ServiceConfigurationContext(services, configuration, builder);
    }
}
