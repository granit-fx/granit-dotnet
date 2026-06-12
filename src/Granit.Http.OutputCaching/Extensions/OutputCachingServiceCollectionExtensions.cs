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
    /// Registers the Granit output caching system with privacy-safe, tenant-aware defaults.
    /// </summary>
    /// <remarks>
    /// Configured services:
    /// <list type="bullet">
    ///   <item>ASP.NET Core <c>OutputCache</c> with base policy chain (private-response + tenant isolation)</item>
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

        services.AddOutputCache();

        // Policy configuration lives here (not in AddOutputCache) so it can honour the
        // bound OutputCachingOptions: the privacy/tenant toggles and the VaryByQuery keys.
        services
            .AddOptions<OutputCacheOptions>()
            .Configure<IOptions<OutputCachingOptions>>((outputCache, granitOptsAccessor) =>
            {
                OutputCachingOptions granitOpts = granitOptsAccessor.Value;

                outputCache.DefaultExpirationTimeSpan = granitOpts.DefaultExpiration;

                // Base policy: applied to all cached endpoints.
                outputCache.AddBasePolicy(builder => ConfigureGranitPolicy(builder, granitOpts));

                // Named policy: explicit Granit default with the same base chain.
                outputCache.AddPolicy(
                    GranitOutputCachePolicyNames.Default,
                    builder => ConfigureGranitPolicy(builder, granitOpts));

                // Named policy: explicitly disable caching for an endpoint.
                outputCache.AddPolicy(GranitOutputCachePolicyNames.NoCache, builder => builder.NoCache());
            });

        services.TryAddSingleton<IOutputCacheEvictionService, OutputCacheEvictionService>();

        return services;
    }

    // Applies the Granit base chain, honouring OutputCachingOptions: private-response
    // isolation and tenant isolation are each opt-out via their toggle, and cache keys
    // vary by the configured query parameters.
    private static void ConfigureGranitPolicy(OutputCachePolicyBuilder builder, OutputCachingOptions options)
    {
        if (options.ExcludeAuthenticatedResponses)
        {
            builder.AddPolicy<PrivateResponseOutputCachePolicy>();
        }

        if (options.EnableTenantIsolation)
        {
            builder.AddPolicy<TenantAwareOutputCachePolicy>();
        }

        builder.SetVaryByQuery(options.VaryByQueryKeys);
        builder.Tag("all");
    }
}
