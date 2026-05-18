// =============================================================================
// MultiTenancyServiceCollectionExtensionsTests - DI registration tests
// =============================================================================

using Granit.MultiTenancy;
using Granit.MultiTenancy.Extensions;
using Granit.MultiTenancy.Internal;
using Granit.MultiTenancy.Middleware;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Resolvers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Tests;

public sealed class MultiTenancyServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(IDictionary<string, string?>? config = null)
    {
        ServiceCollection services = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? new Dictionary<string, string?>())
            .Build();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddMetrics();
        // Middleware needs IStringLocalizer<>; this fixture bypasses the module
        // bootstrap that would activate AddGranitLocalization() via [DependsOn].
        // The fully-wired bootstrap is exercised in GranitMultiTenancyModuleTests.
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(NullStringLocalizer<>));
        services.AddGranitMultiTenancy();
        return services.BuildServiceProvider();
    }

    /// <summary>Null IStringLocalizer used in fixtures that don't need real text.</summary>
    private sealed class NullStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, name, resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
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
