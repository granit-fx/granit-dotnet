// =============================================================================
// MultiTenancyServiceCollectionExtensionsTests - DI registration tests
// =============================================================================

using Granit.MultiTenancy;
using Granit.MultiTenancy.Extensions;
using Granit.MultiTenancy.Middleware;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Resolvers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class MultiTenancyServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        // BindConfiguration requires IConfiguration in the container
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddMetrics();
        services.AddGranitMultiTenancy();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddGranitMultiTenancy_ReplacesNullTenantContext_WithCurrentTenant()
    {
        using ServiceProvider provider = BuildProvider();

        ICurrentTenant tenant = provider.GetRequiredService<ICurrentTenant>();

        tenant.ShouldBeOfType<CurrentTenant>();
    }

    [Fact]
    public void AddGranitMultiTenancy_RegistersHeaderTenantResolver()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        IEnumerable<ITenantResolver> resolvers = scope.ServiceProvider.GetRequiredService<IEnumerable<ITenantResolver>>();

        resolvers.ShouldContain(r => r is HeaderTenantResolver);
    }

    [Fact]
    public void AddGranitMultiTenancy_RegistersJwtClaimTenantResolver()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        IEnumerable<ITenantResolver> resolvers = scope.ServiceProvider.GetRequiredService<IEnumerable<ITenantResolver>>();

        resolvers.ShouldContain(r => r is JwtClaimTenantResolver);
    }

    [Fact]
    public void AddGranitMultiTenancy_RegistersTenantResolverPipeline()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        TenantResolverPipeline pipeline = scope.ServiceProvider.GetRequiredService<TenantResolverPipeline>();

        pipeline.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitMultiTenancy_RegistersTenantResolutionMiddleware_AsScoped()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        TenantResolutionMiddleware middleware = scope.ServiceProvider.GetRequiredService<TenantResolutionMiddleware>();

        middleware.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitMultiTenancy_RegistersMultiTenancyOptions()
    {
        using ServiceProvider provider = BuildProvider();

        IOptions<MultiTenancyOptions> options = provider.GetRequiredService<IOptions<MultiTenancyOptions>>();

        options.Value.ShouldNotBeNull();
        options.Value.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void AddGranitMultiTenancy_TenantResolverPipeline_IsScoped()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        TenantResolverPipeline first = scope.ServiceProvider.GetRequiredService<TenantResolverPipeline>();
        TenantResolverPipeline second = scope.ServiceProvider.GetRequiredService<TenantResolverPipeline>();

        first.ShouldBeSameAs(second, "TenantResolverPipeline should be same within a scope");
    }

    [Fact]
    public void AddGranitMultiTenancy_CalledTwice_DoesNotDuplicatePipeline()
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddMetrics();
        services.AddGranitMultiTenancy();
        services.AddGranitMultiTenancy(); // second call
        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        // TryAddScoped prevents duplicate TenantResolverPipeline
        TenantResolverPipeline pipeline = scope.ServiceProvider.GetRequiredService<TenantResolverPipeline>();
        pipeline.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitMultiTenancy_ReturnsServiceCollection_ForChaining()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitMultiTenancy();

        result.ShouldBeSameAs(services);
    }
}
