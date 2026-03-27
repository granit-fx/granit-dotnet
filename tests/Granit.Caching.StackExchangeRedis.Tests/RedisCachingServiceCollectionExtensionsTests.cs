// =============================================================================
// Tests - RedisCachingServiceCollectionExtensions
// =============================================================================
// Vérifie que AddGranitCachingRedis :
//   - Enregistre toujours RedisCache comme IDistributedCache
//   - Enregistre AesCacheValueEncryptor si EncryptValues = true
//   - Conserve NullCacheValueEncryptor si EncryptValues = false
//   - Configure correctement les options Redis et StackExchange
// =============================================================================

using Granit.Caching.Options;
using Granit.Caching.StackExchangeRedis.Extensions;
using Granit.Caching.StackExchangeRedis.HealthChecks;
using Granit.Caching.StackExchangeRedis.Options;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Granit.Caching.StackExchangeRedis.Tests;

public sealed class RedisCachingServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    [Fact]
    public void AddGranitCachingRedis_AlwaysRegistersRedisCache()
    {
        // Arrange
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Cache:Redis:Configuration"] = "localhost:6379",
            ["Cache:Redis:InstanceName"] = "test:",
        });

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);

        // Act
        services.AddGranitCachingRedis();

        // Assert — RedisCache doit être enregistré pour IDistributedCache
        ServiceDescriptor? redisDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IDistributedCache));
        redisDescriptor.ShouldNotBeNull("Redis doit être enregistré comme IDistributedCache");
    }

    [Fact]
    public void AddGranitCachingRedis_EncryptValues_True_RegistersAesEncryptor()
    {
        // Arrange — a valid 256-bit (32-byte) non-zero AES key encoded in base64
        byte[] keyBytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(keyBytes);
        string testAesKey = Convert.ToBase64String(keyBytes);

        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Cache:Redis:Configuration"] = "localhost:6379",
            ["Cache:EncryptValues"] = "true",
            ["Cache:Encryption:Key"] = testAesKey,
        });

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);

        // Register CachingOptions and CacheEncryptionOptions (normally done by AddGranitCaching)
        services
            .AddOptions<CachingOptions>()
            .BindConfiguration(CachingOptions.SectionName)
            .ValidateDataAnnotations();
        services
            .AddOptions<CacheEncryptionOptions>()
            .BindConfiguration(CacheEncryptionOptions.SectionName)
            .ValidateDataAnnotations();

        // Act
        services.AddGranitCachingRedis();

        // Assert — factory resolves to AesCacheValueEncryptor at runtime
        using ServiceProvider sp = services.BuildServiceProvider();
        ICacheValueEncryptor encryptor = sp.GetRequiredService<ICacheValueEncryptor>();
        encryptor.ShouldBeOfType<AesCacheValueEncryptor>();
    }

    [Fact]
    public void AddGranitCachingRedis_EncryptValues_False_RegistersNullEncryptor()
    {
        // Arrange
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Cache:Redis:Configuration"] = "localhost:6379",
            ["Cache:EncryptValues"] = "false",
        });

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);

        // Register CachingOptions (normally done by AddGranitCaching)
        services
            .AddOptions<CachingOptions>()
            .BindConfiguration(CachingOptions.SectionName)
            .ValidateDataAnnotations();

        // Act
        services.AddGranitCachingRedis();

        // Assert — factory resolves to NullCacheValueEncryptor at runtime
        using ServiceProvider sp = services.BuildServiceProvider();
        ICacheValueEncryptor encryptor = sp.GetRequiredService<ICacheValueEncryptor>();
        encryptor.ShouldBeOfType<NullCacheValueEncryptor>();
    }

    [Fact]
    public void AddGranitCachingRedis_ConfiguresRedisCachingOptions()
    {
        // Arrange
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Cache:Redis:Configuration"] = "redis-service:6379",
            ["Cache:Redis:InstanceName"] = "test:",
        });

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddGranitCachingRedis();
        ServiceProvider sp = services.BuildServiceProvider();

        // Act
        RedisCachingOptions opts = sp.GetRequiredService<IOptions<RedisCachingOptions>>().Value;

        // Assert
        opts.Configuration.ShouldBe("redis-service:6379");
        opts.InstanceName.ShouldBe("test:");
    }

    [Fact]
    public void AddGranitCachingRedis_AppliesRedisConfigurationToStackExchangeOptions()
    {
        // Arrange
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Cache:Redis:Configuration"] = "redis-service:6379",
            ["Cache:Redis:InstanceName"] = "myapp:",
        });

        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddGranitCachingRedis();
        ServiceProvider sp = services.BuildServiceProvider();

        // Act — résoudre IOptions<RedisCacheOptions> force l'exécution du lambda de configuration
        RedisCacheOptions redisOpts = sp.GetRequiredService<IOptions<RedisCacheOptions>>().Value;

        // Assert
        redisOpts.Configuration.ShouldBe("redis-service:6379");
        redisOpts.InstanceName.ShouldBe("myapp:");
    }

    [Fact]
    public void AddGranitRedisHealthCheck_WhenIConnectionMultiplexerNotRegistered_RegistersIt()
    {
        // Arrange
        ServiceCollection services = new();
        // Configure RedisCachingOptions so the factory lambda can read it
        services.Configure<RedisCachingOptions>(opts => opts.Configuration = "localhost:6379");
        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Act
        builder.AddGranitRedisHealthCheck();

        // Assert — IConnectionMultiplexer must have been registered
        ServiceDescriptor? multiplexerDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(IConnectionMultiplexer));
        multiplexerDescriptor.ShouldNotBeNull();
        multiplexerDescriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitRedisHealthCheck_WhenIConnectionMultiplexerAlreadyRegistered_DoesNotRegisterAgain()
    {
        // Arrange
        ServiceCollection services = new();
        IConnectionMultiplexer existingMultiplexer = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(existingMultiplexer);
        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Act
        builder.AddGranitRedisHealthCheck();

        // Assert — only one IConnectionMultiplexer registration
        IEnumerable<ServiceDescriptor> multiplexerDescriptors = services.Where(
            d => d.ServiceType == typeof(IConnectionMultiplexer));
        multiplexerDescriptors.Count().ShouldBe(1);
    }

    [Fact]
    public void AddGranitRedisHealthCheck_RegistersCheckTaggedReadiness()
    {
        // Arrange
        ServiceCollection services = new();
        IConnectionMultiplexer existingMultiplexer = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(existingMultiplexer);
        IHealthChecksBuilder builder = services.AddHealthChecks();

        // Act
        builder.AddGranitRedisHealthCheck(name: "redis");

        // Assert — HealthCheckRegistration tagged "readiness" and "startup"
        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions opts = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;
        HealthCheckRegistration? registration = opts.Registrations.FirstOrDefault(r => r.Name == "redis");
        registration.ShouldNotBeNull();
        registration!.Tags.ShouldContain("readiness");
        registration.Tags.ShouldContain("startup");
    }
}
