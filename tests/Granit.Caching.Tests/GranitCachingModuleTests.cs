// =============================================================================
// Tests - GranitCachingModule
// =============================================================================
// Verifies that the module registers:
//   - CachingOptions (bound from configuration)
//   - CacheEncryptionOptions (bound from configuration)
//   - ICacheValueEncryptor → NullCacheValueEncryptor (default)
// =============================================================================

using Granit.Caching.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Caching.Tests;

public sealed class GranitCachingModuleTests
{
    private static ServiceProvider BuildServiceProvider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        GranitCachingModule module = new();
        Granit.Modularity.ServiceConfigurationContext context = new(
            builder.Services,
            builder.Configuration,
            builder);

        module.ConfigureServices(context);
        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void ConfigureServices_RegistersCachingOptions()
    {
        // Arrange & Act
        ServiceProvider sp = BuildServiceProvider();

        // Assert
        IOptions<CachingOptions> options = sp.GetRequiredService<IOptions<CachingOptions>>();
        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersCacheEncryptionOptions()
    {
        // Arrange & Act
        ServiceProvider sp = BuildServiceProvider();

        // Assert
        IOptions<CacheEncryptionOptions> options = sp.GetRequiredService<IOptions<CacheEncryptionOptions>>();
        options.Value.ShouldNotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersNullEncryptor_ByDefault()
    {
        // Arrange & Act
        ServiceProvider sp = BuildServiceProvider();

        // Assert — in the absence of EncryptValues configuration, the no-op encryptor is used
        ICacheValueEncryptor encryptor = sp.GetRequiredService<ICacheValueEncryptor>();
        encryptor.ShouldBeOfType<NullCacheValueEncryptor>();
    }
}
