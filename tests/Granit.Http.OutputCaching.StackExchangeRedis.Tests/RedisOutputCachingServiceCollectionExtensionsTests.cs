using Granit.Http.OutputCaching.StackExchangeRedis.Extensions;
using Granit.Http.OutputCaching.StackExchangeRedis.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.StackExchangeRedis.Tests;

public sealed class RedisOutputCachingServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitRedisOutputCache_RegistersRedisOutputCachingOptions()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Services.AddGranitRedisOutputCache();

        using ServiceProvider sp = builder.Services.BuildServiceProvider();

        // Act
        RedisOutputCachingOptions options =
            sp.GetRequiredService<IOptions<RedisOutputCachingOptions>>().Value;

        // Assert
        options.IsEnabled.ShouldBeTrue();
        options.Configuration.ShouldBe("localhost:6379");
        options.InstanceName.ShouldBe("dd:oc:");
        options.RequireTls.ShouldBeTrue();
    }
}
