using Granit.Modularity;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Caching.StackExchangeRedis.Tests;

public sealed class GranitCachingStackExchangeRedisModuleIsEnabledTests
{
    [Fact]
    public void IsEnabled_RedisEnabledTrue_ReturnsTrue()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Cache:Redis:IsEnabled"] = "true";
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        GranitCachingStackExchangeRedisModule module = new();

        // Act
        bool result = module.IsEnabled(context);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_RedisEnabledFalse_ReturnsFalse()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Cache:Redis:IsEnabled"] = "false";
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        GranitCachingStackExchangeRedisModule module = new();

        // Act
        bool result = module.IsEnabled(context);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void IsEnabled_NoRedisSection_ReturnsTrue_BecauseDefaultIsEnabled()
    {
        // Arrange — no Cache:Redis section at all, defaults to IsEnabled=true
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        GranitCachingStackExchangeRedisModule module = new();

        // Act
        bool result = module.IsEnabled(context);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void DependsOn_GranitCachingModule()
    {
        // Assert — verify [DependsOn] attribute
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitCachingStackExchangeRedisModule), typeof(DependsOnAttribute));

        attr.ShouldNotBeNull();
        attr!.DependedTypes.ShouldContain(typeof(GranitCachingModule));
    }
}
