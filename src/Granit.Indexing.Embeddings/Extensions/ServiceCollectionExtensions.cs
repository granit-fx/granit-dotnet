using Granit.Diagnostics;
using Granit.Indexing.Embeddings.Diagnostics;
using Granit.Indexing.Embeddings.Internal;
using Granit.Indexing.Embeddings.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Indexing.Embeddings.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Indexing.Embeddings</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the embeddings module wiring: options binding, metrics, activity-source
    /// registration. Does NOT yet decorate <see cref="IIndexer{TKey}"/> or
    /// <see cref="ISearchBackend{TKey, TResult}"/> — call the per-key extensions below
    /// AFTER the host has registered its concrete backend (EF, Elasticsearch, …).
    /// </summary>
    public static IServiceCollection AddGranitIndexingEmbeddings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        GranitActivitySourceRegistry.Register(EmbeddingsActivitySource.Name);

        services.AddOptions<GranitIndexingEmbeddingsOptions>()
            .BindConfiguration(GranitIndexingEmbeddingsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<EmbeddingsMetrics>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }

    /// <summary>
    /// Decorates the host's <see cref="IIndexer{TKey}"/> with
    /// <c>EmbeddingIndexer&lt;TKey&gt;</c> so every <c>IndexAsync</c> call enriches the
    /// entry with an embedding generated via the configured
    /// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MUST be called AFTER the storage backend's <c>Add…</c> extension so the
    /// inner indexer is present in the descriptor list. Fast-fails at composition
    /// time when no <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> is registered
    /// — a silent wiring miss would let entries persist without embeddings and
    /// semantic search would return zero results.
    /// </para>
    /// <para>
    /// MUST also be the LAST decorator on <see cref="IIndexer{TKey}"/>. If a host
    /// chains additional decorators (telemetry, caching), apply them BEFORE calling
    /// this extension so the embedding write reaches the storage backend.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGranitIndexingEmbeddingsWriter<TKey>(this IServiceCollection services)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.All(d => d.ServiceType != typeof(IEmbeddingGenerator<string, Embedding<float>>)))
        {
            throw new InvalidOperationException(
                $"AddGranitIndexingEmbeddingsWriter<{typeof(TKey).Name}>() requires an "
                + "IEmbeddingGenerator<string, Embedding<float>> to be registered in DI. "
                + "Wire your provider (e.g. OpenAI, Azure OpenAI, Ollama) before calling this extension.");
        }

        ServiceDescriptor inner = services.LastOrDefault(d => d.ServiceType == typeof(IIndexer<TKey>))
            ?? throw new InvalidOperationException(
                $"AddGranitIndexingEmbeddingsWriter<{typeof(TKey).Name}>() requires an "
                + $"IIndexer<{typeof(TKey).Name}> to be registered first. Call your storage backend's "
                + "Add… extension before this one (e.g. AddGranitIndexingEntityFrameworkCore).");

        services.Remove(inner);

        services.Add(ServiceDescriptor.Describe(
            typeof(IIndexer<TKey>),
            sp =>
            {
                var innerInstance = (IIndexer<TKey>)CreateFromDescriptor(sp, inner);
                return ActivatorUtilities.CreateInstance<EmbeddingIndexer<TKey>>(sp, innerInstance);
            },
            inner.Lifetime));

        return services;
    }

    /// <summary>
    /// Decorates the host's <see cref="ISearchBackend{TKey, TResult}"/> with
    /// <c>HybridSearchBackend&lt;TKey, TResult&gt;</c> so every search runs the lexical
    /// (BM25 / tsvector) AND semantic (cosine kNN) channels in parallel and fuses the
    /// results via Reciprocal Rank Fusion.
    /// </summary>
    /// <remarks>
    /// Requires both an inner <see cref="ISearchBackend{TKey, TResult}"/> AND an
    /// <see cref="IVectorSearchBackend{TKey, TResult}"/> registered first; throws fast-fail
    /// at composition otherwise.
    /// </remarks>
    public static IServiceCollection AddGranitIndexingHybridSearch<TKey, TResult>(this IServiceCollection services)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.All(d => d.ServiceType != typeof(IEmbeddingGenerator<string, Embedding<float>>)))
        {
            throw new InvalidOperationException(
                $"AddGranitIndexingHybridSearch<{typeof(TKey).Name}, {typeof(TResult).Name}>() requires an "
                + "IEmbeddingGenerator<string, Embedding<float>> to be registered in DI.");
        }

        if (services.All(d => d.ServiceType != typeof(IVectorSearchBackend<TKey, TResult>)))
        {
            throw new InvalidOperationException(
                $"AddGranitIndexingHybridSearch<{typeof(TKey).Name}, {typeof(TResult).Name}>() requires an "
                + $"IVectorSearchBackend<{typeof(TKey).Name}, {typeof(TResult).Name}> to be registered first. "
                + "Wire your storage backend's vector extension (e.g. AddGranitIndexingEmbeddingsBackend for EF, "
                + "AddGranitIndexingElasticsearchVectorBackend for ES) before this one.");
        }

        ServiceDescriptor inner = services.LastOrDefault(d => d.ServiceType == typeof(ISearchBackend<TKey, TResult>))
            ?? throw new InvalidOperationException(
                $"AddGranitIndexingHybridSearch<{typeof(TKey).Name}, {typeof(TResult).Name}>() requires an "
                + $"ISearchBackend<{typeof(TKey).Name}, {typeof(TResult).Name}> to be registered first.");

        services.Remove(inner);

        services.Add(ServiceDescriptor.Describe(
            typeof(ISearchBackend<TKey, TResult>),
            sp =>
            {
                var innerInstance = (ISearchBackend<TKey, TResult>)CreateFromDescriptor(sp, inner);
                IVectorSearchBackend<TKey, TResult> vector = sp.GetRequiredService<IVectorSearchBackend<TKey, TResult>>();
                return ActivatorUtilities.CreateInstance<HybridSearchBackend<TKey, TResult>>(sp, innerInstance, vector);
            },
            inner.Lifetime));

        return services;
    }

    private static object CreateFromDescriptor(IServiceProvider sp, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }
        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(sp);
        }
        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType);
        }
        throw new InvalidOperationException(
            $"ServiceDescriptor for {descriptor.ServiceType.Name} has no instance, factory or type to materialise.");
    }
}
