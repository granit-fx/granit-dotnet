using Granit.Diagnostics;
using Granit.Indexing.Diagnostics;
using Granit.Indexing.Internal;
using Granit.Indexing.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Indexing.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Indexing</c> runtime services.
/// </summary>
/// <remarks>
/// Concrete backends ship in dedicated packages (<c>Granit.Indexing.EntityFrameworkCore</c>
/// for Postgres tsvector, <c>Granit.Indexing.Elasticsearch</c> for ES). Concrete language
/// detectors / summarisers / auto-taggers ship in <c>Granit.Indexing.Lingua</c> and the
/// AI provider packages.
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the indexing module: options, metrics, activity-source registration,
    /// the default composite language detector, the null-object authorizer (override
    /// per-resource ACL by registering a custom <see cref="ISearchResultAuthorizer{TKey}"/>
    /// for each <c>TKey</c>), and the default search-service orchestrator factory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ISearchService{TKey, TResult}"/> is registered as an open generic that
    /// resolves to <c>DefaultSearchService&lt;TKey, TResult&gt;</c>. Consumers register
    /// an <see cref="ISearchBackend{TKey, TResult}"/> for their concrete
    /// <c>(TKey, TResult)</c> tuple — backend packages do this automatically when their
    /// <c>Add…</c> extension is called.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGranitIndexing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        GranitActivitySourceRegistry.Register(IndexingActivitySource.Name);

        services.AddOptions<GranitIndexingOptions>()
            .BindConfiguration(GranitIndexingOptions.SectionName);

        services.TryAddSingleton(sp =>
            sp.GetRequiredService<IOptions<GranitIndexingOptions>>().Value);

        services.TryAddSingleton<IndexingMetrics>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICurrentTenant>(NullTenantContext.Instance);

        // Empty-result rate limiter — in-memory by default; consumers replace by registering
        // an alternative IEmptyResultRateLimiter before calling AddGranitIndexing if needed.
        services.TryAddSingleton<IEmptyResultRateLimiter, EmptyResultRateLimiter>();

        // Default authorizer (null-object). Consumers override per-TKey by registering
        // their own ISearchResultAuthorizer<TKey> AFTER this call.
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ISearchResultAuthorizer<>), typeof(NullSearchResultAuthorizer<>)));

        // Composite language detector — picks up every ILanguageDetector via DI.
        services.TryAddSingleton<CompositeLanguageDetector>();
        services.TryAddSingleton<ILanguageDetector>(sp => sp.GetRequiredService<CompositeLanguageDetector>());

        // Open-generic search-service orchestrator.
        services.TryAdd(ServiceDescriptor.Scoped(typeof(ISearchService<,>), typeof(DefaultSearchService<,>)));

        return services;
    }
}
