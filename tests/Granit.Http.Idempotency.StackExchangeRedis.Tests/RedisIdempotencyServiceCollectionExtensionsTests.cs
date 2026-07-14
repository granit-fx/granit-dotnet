using Granit.Caching;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Extensions;
using Granit.Http.Idempotency.StackExchangeRedis.Extensions;
using Granit.Http.Idempotency.StackExchangeRedis.Internal;
using Granit.Http.Idempotency.StackExchangeRedis.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Granit.Http.Idempotency.StackExchangeRedis.Tests;

public sealed class RedisIdempotencyServiceCollectionExtensionsTests
{
    private static ServiceCollection NewServices(Dictionary<string, string?>? config = null)
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? [])
            .Build());
        return services;
    }

    private static string NewBase64Key() =>
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    // -------------------------------------------------------------------------
    // Store replacement
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitRedisIdempotency_AfterBaseRegistration_ReplacesInMemoryStore()
    {
        ServiceCollection services = NewServices();
        services.AddGranitIdempotency();

        services.AddGranitRedisIdempotency();

        List<ServiceDescriptor> stores = [.. services.Where(d => d.ServiceType == typeof(IIdempotencyStore))];
        stores.Count.ShouldBe(1, "Replace must remove the in-memory descriptor");
        stores[0].ImplementationType.ShouldBe(typeof(RedisIdempotencyStore));
        stores[0].Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddGranitRedisIdempotency_BeforeBaseRegistration_StaysAuthoritative()
    {
        ServiceCollection services = NewServices();
        services.AddGranitRedisIdempotency();

        services.AddGranitIdempotency(); // base TryAdd must no-op

        List<ServiceDescriptor> stores = [.. services.Where(d => d.ServiceType == typeof(IIdempotencyStore))];
        stores.Count.ShouldBe(1);
        stores[0].ImplementationType.ShouldBe(typeof(RedisIdempotencyStore));
    }

    // -------------------------------------------------------------------------
    // Multiplexer reuse
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitRedisIdempotency_WithExistingMultiplexer_DoesNotRegisterAnother()
    {
        ServiceCollection services = NewServices();
        IConnectionMultiplexer existing = Substitute.For<IConnectionMultiplexer>();
        services.AddSingleton(existing);

        services.AddGranitRedisIdempotency();

        services.Count(d => d.ServiceType == typeof(IConnectionMultiplexer)).ShouldBe(1);
    }

    [Fact]
    public void AddGranitRedisIdempotency_WithoutMultiplexer_RegistersOne()
    {
        ServiceCollection services = NewServices();

        services.AddGranitRedisIdempotency();

        services.Count(d => d.ServiceType == typeof(IConnectionMultiplexer)).ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // Options binding
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitRedisIdempotency_BindsOptionsFromConfiguration()
    {
        ServiceCollection services = NewServices(new Dictionary<string, string?>
        {
            ["Http:Idempotency:Redis:Configuration"] = "redis-service:6380",
            ["Http:Idempotency:Redis:InstanceName"] = "myapp:",
            ["Http:Idempotency:Redis:RequireTls"] = "false",
        });

        services.AddGranitRedisIdempotency();

        // Options validation resolves the encryption startup validator, which needs the
        // host environment — always present in a real host.
        Microsoft.Extensions.Hosting.IHostEnvironment environment =
            Substitute.For<Microsoft.Extensions.Hosting.IHostEnvironment>();
        environment.EnvironmentName.Returns(Microsoft.Extensions.Hosting.Environments.Development);
        services.AddSingleton(environment);

        using ServiceProvider sp = services.BuildServiceProvider();
        RedisIdempotencyOptions opts = sp.GetRequiredService<IOptions<RedisIdempotencyOptions>>().Value;

        opts.Configuration.ShouldBe("redis-service:6380");
        opts.InstanceName.ShouldBe("myapp:");
        opts.RequireTls.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Encryptor pipeline (reused from Granit.Caching — no new crypto)
    // -------------------------------------------------------------------------

    [Fact]
    public void Encryptor_WithResolvableKey_IsAes()
    {
        ServiceCollection services = NewServices(new Dictionary<string, string?>
        {
            ["Cache:Encryption:Key"] = NewBase64Key(),
        });

        services.AddGranitRedisIdempotency();

        using ServiceProvider sp = services.BuildServiceProvider();
        sp.GetRequiredService<ICacheValueEncryptor>().ShouldBeOfType<AesCacheValueEncryptor>();
    }

    [Fact]
    public void Encryptor_WithoutKey_IsNoOp()
    {
        ServiceCollection services = NewServices();

        services.AddGranitRedisIdempotency();

        using ServiceProvider sp = services.BuildServiceProvider();
        sp.GetRequiredService<ICacheValueEncryptor>().ShouldBeOfType<NullCacheValueEncryptor>();
    }

    [Fact]
    public void Encryptor_PreRegistered_IsKeptAuthoritative()
    {
        // When Granit.Caching.StackExchangeRedis already registered the encryptor,
        // the idempotency provider must not shadow it (TryAdd semantics).
        ServiceCollection services = NewServices();
        ICacheValueEncryptor existing = Substitute.For<ICacheValueEncryptor>();
        services.AddSingleton(existing);

        services.AddGranitRedisIdempotency();

        using ServiceProvider sp = services.BuildServiceProvider();
        sp.GetRequiredService<ICacheValueEncryptor>().ShouldBeSameAs(existing);
    }

    // -------------------------------------------------------------------------
    // Fail-closed encryption validator
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitRedisIdempotency_RegistersEncryptionStartupValidator()
    {
        ServiceCollection services = NewServices();

        services.AddGranitRedisIdempotency();

        services.ShouldContain(d =>
            d.ServiceType == typeof(IValidateOptions<RedisIdempotencyOptions>) &&
            d.ImplementationType == typeof(RedisIdempotencyEncryptionStartupValidator));
    }

    // -------------------------------------------------------------------------
    // Health check
    // -------------------------------------------------------------------------

    [Fact]
    public void AddGranitRedisIdempotencyHealthCheck_RegistersReadinessAndStartupTags()
    {
        ServiceCollection services = NewServices();
        services.AddGranitRedisIdempotency();

        services.AddHealthChecks().AddGranitRedisIdempotencyHealthCheck();

        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        HealthCheckRegistration registration = options.Registrations.ShouldHaveSingleItem();
        registration.Name.ShouldBe("redis-idempotency");
        registration.Tags.ShouldContain("readiness");
        registration.Tags.ShouldContain("startup");
    }

    [Fact]
    public void AddGranitRedisIdempotencyHealthCheck_WithCustomName_UsesIt()
    {
        ServiceCollection services = NewServices();
        services.AddGranitRedisIdempotency();

        services.AddHealthChecks().AddGranitRedisIdempotencyHealthCheck(name: "idp-redis");

        using ServiceProvider sp = services.BuildServiceProvider();
        HealthCheckServiceOptions options = sp.GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value;

        options.Registrations.ShouldHaveSingleItem().Name.ShouldBe("idp-redis");
    }
}
