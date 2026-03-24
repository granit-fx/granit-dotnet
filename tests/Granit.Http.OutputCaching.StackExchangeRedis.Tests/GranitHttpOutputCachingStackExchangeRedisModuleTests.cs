using Granit.Http.OutputCaching.StackExchangeRedis.Options;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.StackExchangeRedis.Tests;

public sealed class GranitHttpOutputCachingStackExchangeRedisModuleTests
{
    [Fact]
    public void IsEnabled_WhenDisabledInConfig_ReturnsFalse()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RedisOutputCachingOptions.SectionName}:IsEnabled"] = "false",
            })
            .Build();

        var module = new GranitHttpOutputCachingStackExchangeRedisModule();
        Granit.Modularity.ServiceConfigurationContext context = new(
            new Microsoft.Extensions.DependencyInjection.ServiceCollection(),
            configuration,
            null!);

        // Act
        bool isEnabled = module.IsEnabled(context);

        // Assert
        isEnabled.ShouldBeFalse();
    }

    [Fact]
    public void IsEnabled_WhenEnabledByDefault_ReturnsTrue()
    {
        // Arrange
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        var module = new GranitHttpOutputCachingStackExchangeRedisModule();
        Granit.Modularity.ServiceConfigurationContext context = new(
            new Microsoft.Extensions.DependencyInjection.ServiceCollection(),
            configuration,
            null!);

        // Act
        bool isEnabled = module.IsEnabled(context);

        // Assert
        isEnabled.ShouldBeTrue();
    }
}
