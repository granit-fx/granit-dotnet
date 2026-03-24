using Granit.Http.ExceptionHandling;
using Granit.RateLimiting.Abstractions;
using Granit.RateLimiting.Diagnostics;
using Granit.RateLimiting.Exceptions;
using Granit.RateLimiting.Extensions;
using Granit.RateLimiting.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class RateLimitingServiceCollectionExtensionsTests
{
    private static ServiceCollection CreateServices()
    {
        ServiceCollection services = new();
        services.AddSingleton(TimeProvider.System);
        services.AddMetrics();

        // BindConfiguration requires IConfiguration in DI
        IConfiguration config = new ConfigurationBuilder().Build();
        services.AddSingleton(config);

        return services;
    }

    [Fact]
    public void AddGranitRateLimiting_RegistersRequiredServices()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitRateLimiting(opts =>
        {
            opts.KeyPrefix = "test";
            opts.Policies["api"] = new RateLimitPolicyOptions { PermitLimit = 100 };
        });

        ServiceProvider sp = services.BuildServiceProvider();

        sp.GetService<IOptions<GranitRateLimitingOptions>>().ShouldNotBeNull();
        sp.GetService<RateLimitingMetrics>().ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitRateLimiting_RegistersExceptionStatusCodeMapper()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitRateLimiting();

        ServiceProvider sp = services.BuildServiceProvider();

        IEnumerable<IExceptionStatusCodeMapper> mappers = sp.GetServices<IExceptionStatusCodeMapper>();
        mappers.ShouldContain(m => m is RateLimitExceptionStatusCodeMapper);
    }

    [Fact]
    public void AddGranitRateLimiting_WithConfigureAction_AppliesConfiguration()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitRateLimiting(opts =>
        {
            opts.KeyPrefix = "custom";
            opts.Enabled = false;
        });

        ServiceProvider sp = services.BuildServiceProvider();
        GranitRateLimitingOptions options = sp.GetRequiredService<IOptions<GranitRateLimitingOptions>>().Value;

        options.KeyPrefix.ShouldBe("custom");
        options.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void AddGranitRateLimiting_WithNullConfigure_DoesNotThrow()
    {
        ServiceCollection services = CreateServices();

        Should.NotThrow(() => services.AddGranitRateLimiting(configure: null));
    }

    [Fact]
    public void AddGranitRateLimiting_WithConfigurationSection_BindsOptions()
    {
        Dictionary<string, string?> configData = new()
        {
            ["RateLimiting:Enabled"] = "false",
            ["RateLimiting:KeyPrefix"] = "bound",
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(TimeProvider.System);
        services.AddMetrics();

        services.AddGranitRateLimiting(configuration.GetSection("RateLimiting"));

        ServiceProvider sp = services.BuildServiceProvider();
        GranitRateLimitingOptions options = sp.GetRequiredService<IOptions<GranitRateLimitingOptions>>().Value;

        options.Enabled.ShouldBeFalse();
        options.KeyPrefix.ShouldBe("bound");
    }

    [Fact]
    public void AddGranitRateLimiting_WithoutRedis_ResolvesInMemoryCounterStore()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitRateLimiting(opts =>
        {
            opts.KeyPrefix = "test";
        });

        ServiceProvider sp = services.BuildServiceProvider();

        using IServiceScope scope = sp.CreateScope();
        IRateLimitCounterStore store = scope.ServiceProvider.GetRequiredService<IRateLimitCounterStore>();

        store.ShouldNotBeNull();
        store.GetType().Name.ShouldBe("InMemoryRateLimitCounterStore");
    }

    [Fact]
    public void AddGranitRateLimiting_RegistersTenantPartitionedRateLimiter()
    {
        ServiceCollection services = CreateServices();
        services.AddLogging();

        // Add required dependencies for TenantPartitionedRateLimiter
        services.AddSingleton(NSubstitute.Substitute.For<Granit.MultiTenancy.ICurrentTenant>());
        services.AddSingleton(NSubstitute.Substitute.For<Granit.Users.ICurrentUserService>());

        services.AddGranitRateLimiting(opts =>
        {
            opts.KeyPrefix = "test";
        });

        ServiceProvider sp = services.BuildServiceProvider();

        using IServiceScope scope = sp.CreateScope();
        TenantPartitionedRateLimiter limiter = scope.ServiceProvider.GetRequiredService<TenantPartitionedRateLimiter>();

        limiter.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitRateLimiting_WithoutFeatureBasedQuotas_ResolvesOptionsProvider()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitRateLimiting(opts =>
        {
            opts.KeyPrefix = "test";
            opts.UseFeatureBasedQuotas = false;
        });

        ServiceProvider sp = services.BuildServiceProvider();

        using IServiceScope scope = sp.CreateScope();
        IRateLimitQuotaProvider provider = scope.ServiceProvider.GetRequiredService<IRateLimitQuotaProvider>();

        provider.ShouldNotBeNull();
        provider.GetType().Name.ShouldBe("OptionsRateLimitQuotaProvider");
    }

    [Fact]
    public void AddGranitRateLimiting_WithFeatureBasedQuotas_ResolvesFeatureBasedProvider()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitRateLimiting(opts =>
        {
            opts.KeyPrefix = "test";
            opts.UseFeatureBasedQuotas = true;
        });

        ServiceProvider sp = services.BuildServiceProvider();

        using IServiceScope scope = sp.CreateScope();
        IRateLimitQuotaProvider provider = scope.ServiceProvider.GetRequiredService<IRateLimitQuotaProvider>();

        provider.ShouldNotBeNull();
        provider.GetType().Name.ShouldBe("FeatureBasedRateLimitQuotaProvider");
    }

    [Fact]
    public void AddGranitRateLimiting_RegistersOptionsValidator()
    {
        ServiceCollection services = CreateServices();

        services.AddGranitRateLimiting();

        ServiceProvider sp = services.BuildServiceProvider();

        IEnumerable<IValidateOptions<GranitRateLimitingOptions>> validators =
            sp.GetServices<IValidateOptions<GranitRateLimitingOptions>>();

        validators.ShouldNotBeEmpty();
    }
}
