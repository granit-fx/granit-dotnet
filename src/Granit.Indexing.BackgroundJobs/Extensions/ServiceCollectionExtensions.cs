using Granit.Indexing.BackgroundJobs.Diagnostics;
using Granit.Indexing.BackgroundJobs.Internal;
using Granit.Indexing.BackgroundJobs.Options;
using Granit.Indexing.BackgroundJobs.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Indexing.BackgroundJobs.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Indexing.BackgroundJobs</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the rebuild-index job infrastructure: options binding, metrics,
    /// activity-source registration, the in-memory checkpoint-store fallback. The
    /// per-<c>TKey</c> rebuild service is registered by
    /// <see cref="AddGranitIndexingRebuildSource{TKey, TSource}"/>.
    /// </summary>
    public static IServiceCollection AddGranitIndexingBackgroundJobs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<IndexingBackgroundJobsOptions>()
            .BindConfiguration(IndexingBackgroundJobsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<IndexingBackgroundJobsMetrics>();

        // In-memory fallback. The EF persistent store registers via TryAdd on the same
        // open generic, so calling AddGranitIndexingEntityFrameworkCoreCheckpointStore
        // BEFORE this extension wins; calling it AFTER also wins because the EF
        // extension uses TryAdd and the in-memory descriptor is replaced explicitly
        // there.
        services.TryAdd(ServiceDescriptor.Singleton(
            typeof(IRebuildCheckpointStore<>),
            typeof(InMemoryRebuildCheckpointStore<>)));

        return services;
    }

    /// <summary>
    /// Registers a concrete <see cref="IIndexedEntrySource{TKey}"/> implementation and
    /// the matching <see cref="RebuildIndexService{TKey}"/>. Hosts call this once per
    /// <typeparamref name="TKey"/> they want to rebuild.
    /// </summary>
    public static IServiceCollection AddGranitIndexingRebuildSource<TKey, TSource>(
        this IServiceCollection services)
        where TKey : notnull
        where TSource : class, IIndexedEntrySource<TKey>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IIndexedEntrySource<TKey>, TSource>();
        services.TryAddScoped<RebuildIndexService<TKey>>();

        return services;
    }
}
