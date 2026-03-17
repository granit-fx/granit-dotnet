using Granit.Http.OutputCaching.Eviction;
using Granit.Http.OutputCaching.Options;
using Granit.Http.OutputCaching.Policies;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Http.OutputCaching.Extensions;

/// <summary>
/// DI registration extensions for <c>Granit.Http.OutputCaching</c>.
/// </summary>
public static class OutputCachingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Granit output caching system with GDPR-safe, tenant-aware defaults.
    /// </summary>
    /// <remarks>
    /// Configured services:
    /// <list type="bullet">
    ///   <item>ASP.NET Core <c>OutputCache</c> with base policy chain (GDPR + tenant isolation)</item>
    ///   <item><see cref="IOutputCacheEvictionService"/> for tag-based cache invalidation</item>
    ///   <item>Named policies: <see cref="GranitOutputCachePolicyNames.Default"/> and
    ///   <see cref="GranitOutputCachePolicyNames.NoCache"/></item>
    /// </list>
    /// The in-memory <c>IOutputCacheStore</c> is used by default. Install
    /// <c>Granit.Http.OutputCaching.StackExchangeRedis</c> for a distributed Redis backend.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitOutputCaching(this IServiceCollection services)
    {
        services
            .AddOptions<OutputCachingOptions>()
            .BindConfiguration(OutputCachingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOutputCache(options =>
        {
            // Base policy: applied to all cached endpoints
            options.AddBasePolicy(builder =>
            {
                builder.AddPolicy<GdprCompliantOutputCachePolicy>();
                builder.AddPolicy<TenantAwareOutputCachePolicy>();
                builder.Tag("all");
            });

            // Named policy: explicit Granit default with same base chain
            options.AddPolicy(GranitOutputCachePolicyNames.Default, builder =>
            {
                builder.AddPolicy<GdprCompliantOutputCachePolicy>();
                builder.AddPolicy<TenantAwareOutputCachePolicy>();
                builder.Tag("all");
            });

            // Named policy: explicitly disable caching for an endpoint
            options.AddPolicy(GranitOutputCachePolicyNames.NoCache, builder =>
                builder.NoCache());
        });

        // Override default expiration and VaryByQuery from Granit options
        services
            .AddOptions<OutputCacheOptions>()
            .Configure<IOptions<OutputCachingOptions>>((outputCache, granitOpts) =>
            {
                outputCache.DefaultExpirationTimeSpan = granitOpts.Value.DefaultExpiration;
            });

        services.TryAddSingleton<IOutputCacheEvictionService, OutputCacheEvictionService>();

        return services;
    }
}
