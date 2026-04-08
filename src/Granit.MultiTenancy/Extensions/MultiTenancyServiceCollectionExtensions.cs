using Granit.MultiTenancy.Diagnostics;
using Granit.MultiTenancy.Middleware;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Resolvers;
using Granit.MultiTenancy.Stores;
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

        // NullTenantReader fallback — replaced by EfCoreTenantStore when
        // Granit.MultiTenancy.EntityFrameworkCore is in the module tree.
        services.TryAddScoped<ITenantReader, NullTenantReader>();

        // Resolvers: Domain (50) → Header (100) → JWT (200) → QueryString (300)
        // Registered as scoped: DomainTenantResolver depends on ITenantReader (scoped, EF Core).
        // All resolvers aligned to scoped for consistency.
        services.AddScoped<ITenantResolver, DomainTenantResolver>();
        services.AddScoped<ITenantResolver, HeaderTenantResolver>();
        services.AddScoped<ITenantResolver, JwtClaimTenantResolver>();
        services.AddScoped<ITenantResolver, QueryStringTenantResolver>();

        services.TryAddScoped<TenantResolverPipeline>();
        services.TryAddSingleton<MultiTenancyMetrics>();

        // IMiddleware pattern: resolved per scope (per request)
        services.AddScoped<TenantResolutionMiddleware>();

        return services;
    }
}
