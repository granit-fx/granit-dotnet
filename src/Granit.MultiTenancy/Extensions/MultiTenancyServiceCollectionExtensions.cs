using Granit.MultiTenancy.Diagnostics;
using Granit.MultiTenancy.Middleware;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Resolvers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.MultiTenancy.Extensions;

/// <summary>
/// Extensions for configuring MultiTenancy services in the DI container.
/// </summary>
public static class MultiTenancyServiceCollectionExtensions
{
    /// <summary>
    /// Adds MultiTenancy services: ICurrentTenant, resolvers, pipeline, and middleware.
    /// </summary>
    public static IServiceCollection AddGranitMultiTenancy(
        this IServiceCollection services)
    {
        services
            .AddOptions<MultiTenancyOptions>()
            .BindConfiguration(MultiTenancyOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Replace the NullTenantContext registered by AddGranit<T>() with the real implementation.
        services.Replace(ServiceDescriptor.Singleton<ICurrentTenant, CurrentTenant>());

        // Resolvers: Header first (order=100), then JWT (order=200)
        services.AddSingleton<ITenantResolver, HeaderTenantResolver>();
        services.AddSingleton<ITenantResolver, JwtClaimTenantResolver>();

        services.TryAddSingleton<TenantResolverPipeline>();
        services.TryAddSingleton<MultiTenancyMetrics>();

        // IMiddleware pattern: resolved per scope (per request)
        services.AddScoped<TenantResolutionMiddleware>();

        return services;
    }
}
