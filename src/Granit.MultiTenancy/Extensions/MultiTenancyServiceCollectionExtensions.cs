using Granit.DataExchange.Extensions;
using Granit.MultiTenancy.Diagnostics;
using Granit.MultiTenancy.Domain;
using Granit.MultiTenancy.Exports;
using Granit.MultiTenancy.Internal;
using Granit.MultiTenancy.Middleware;
using Granit.MultiTenancy.Options;
using Granit.MultiTenancy.Pipeline;
using Granit.MultiTenancy.Queries;
using Granit.MultiTenancy.Resolvers;
using Granit.MultiTenancy.Stores;
using Granit.MultiTenancy.Url;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

        services.TryAddSingleton<IValidateOptions<MultiTenancyOptions>, MultiTenancyOptionsValidator>();

        // Replace the NullTenantContext registered by AddGranit<T>() with the real
        // implementation. CurrentTenant uses a dual-backend storage: HttpContext.Features
        // when a request is active (ties the tenant lifetime to the request itself,
        // so there is no execution-context state to leak across requests on thread-pool
        // reuse), and an AsyncLocal fallback for non-HTTP code paths (Wolverine handlers,
        // background jobs, migrations).
        services.AddHttpContextAccessor();
        services.Replace(ServiceDescriptor.Singleton<ICurrentTenant, CurrentTenant>());

        // Host-access signal — read by EfStoreBase to distinguish a deliberate
        // .AllowHostAccess() bypass (origin=host_endpoint) from an unsignaled
        // tenant-context loss (origin=implicit_unsignaled, alert-worthy).
        services.TryAddSingleton<IHostAccessContext, HttpContextHostAccessContext>();

        // NullTenantReader fallback — replaced by EfCoreTenantStore when
        // Granit.MultiTenancy.EntityFrameworkCore is in the module tree.
        services.TryAddScoped<ITenantReader, NullTenantReader>();

        // Resolvers: CustomDomain (25) → Domain (50) → Header (100) → JWT (200) → QueryString (300)
        // Registered as scoped: DomainTenantResolver depends on ITenantReader (scoped, EF Core).
        // All resolvers aligned to scoped for consistency.
        services.AddScoped<ITenantResolver, CustomDomainTenantResolver>();
        services.AddScoped<ITenantResolver, DomainTenantResolver>();
        services.AddScoped<ITenantResolver, HeaderTenantResolver>();
        services.AddScoped<ITenantResolver, JwtClaimTenantResolver>();
        services.AddScoped<ITenantResolver, QueryStringTenantResolver>();

        services.TryAddScoped<TenantResolverPipeline>();
        services.TryAddSingleton<MultiTenancyMetrics>();

        // Outbound URL resolution (scoped: depends on ICurrentTenant + ITenantReader)
        services.TryAddScoped<ITenantUrlResolver, TenantUrlResolver>();

        // IMiddleware pattern: resolved per scope (per request)
        services.AddScoped<TenantResolutionMiddleware>();

        services.AddQueryDefinition<Tenant, TenantQueryDefinition>();
        services.AddExportDefinition<Tenant, TenantExportDefinition>();

        return services;
    }
}
