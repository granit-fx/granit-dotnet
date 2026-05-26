using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Granit.Indexing.Elasticsearch.Internal;
using Granit.Indexing.Elasticsearch.Options;
using Granit.Indexing.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Indexing.Elasticsearch.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Indexing.Elasticsearch</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Elasticsearch-backed indexing storage: the
    /// <see cref="ElasticsearchClient"/>, the indexer, the search backend factory, the
    /// GDPR Art. 17 cascade handler, and runtime options. Replaces any previously
    /// registered <see cref="IIndexer{TKey}"/> / <see cref="IIndexedDataEraser"/> for the
    /// supplied key types.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configureClient">
    /// Optional callback to refine the <see cref="ElasticsearchClientSettings"/> (custom
    /// HTTP handlers, request timeouts, certificate validation, …). The framework applies
    /// the URI and API key from
    /// <see cref="IndexingElasticsearchOptions"/> before invoking this callback.
    /// </param>
    /// <param name="indexedKeyTypes">
    /// CLR types of keys this host indexes (e.g. <c>typeof(Guid)</c>). One index — or one
    /// index family, when the per-tenant strategy is selected — is created per type.
    /// </param>
    public static IServiceCollection AddGranitIndexingElasticsearch(
        this IServiceCollection services,
        Action<ElasticsearchClientSettings>? configureClient,
        params Type[] indexedKeyTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(indexedKeyTypes);

        if (indexedKeyTypes.Length == 0)
        {
            throw new ArgumentException(
                "At least one TKey must be registered for the Elasticsearch backend. "
                + "Typical default is typeof(Guid).",
                nameof(indexedKeyTypes));
        }

        services.AddOptions<IndexingElasticsearchOptions>()
            .BindConfiguration(IndexingElasticsearchOptions.SectionName)
            .Validate(opts => !string.IsNullOrWhiteSpace(opts.Uri),
                "Indexing:Elasticsearch:Uri is required.");

        services.TryAddSingleton(sp =>
            sp.GetRequiredService<IOptions<IndexingElasticsearchOptions>>().Value);

        services.TryAddSingleton<IndexNameResolver>();
        services.TryAddSingleton(sp =>
        {
            IndexingElasticsearchOptions options = sp.GetRequiredService<IndexingElasticsearchOptions>();
            ElasticsearchClientSettings settings = new(new Uri(options.Uri));
            if (!string.IsNullOrEmpty(options.ApiKey))
            {
                settings = settings.Authentication(new ApiKey(options.ApiKey));
            }
            configureClient?.Invoke(settings);
            return new ElasticsearchClient(settings);
        });

        services.TryAddSingleton<IndexBootstrapper>();

        Type[] keyTypes = [.. indexedKeyTypes];

        // Replace IIndexer<TKey> registrations — host explicitly opted into ES, the
        // default EF indexer must not also fire on the same dispatch.
        foreach (Type keyType in keyTypes)
        {
            Type indexerService = typeof(IIndexer<>).MakeGenericType(keyType);
            Type indexerImpl = typeof(ElasticsearchIndexer<>).MakeGenericType(keyType);

            for (int i = services.Count - 1; i >= 0; i--)
            {
                if (services[i].ServiceType == indexerService)
                {
                    services.RemoveAt(i);
                }
            }

            services.AddScoped(indexerService, indexerImpl);
        }

        // Replace every existing IIndexedDataEraser — the privacy bridge calls every
        // eraser, so leaving the EF eraser registered would double-cascade. Hosts that
        // want both backends co-existing register them via separate composition roots and
        // accept the dispatch cost.
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(IIndexedDataEraser))
            {
                services.RemoveAt(i);
            }
        }

        services.AddScoped<IIndexedDataEraser>(sp => new ElasticsearchIndexedDataEraser(
            sp.GetRequiredService<ElasticsearchClient>(),
            sp.GetRequiredService<IndexNameResolver>(),
            keyTypes));

        return services;
    }

    /// <summary>
    /// Registers a concrete <see cref="ISearchBackend{TKey, TResult}"/> for the given
    /// <c>(TKey, TResult)</c> tuple, replacing any previously registered backend (EF or
    /// otherwise) for the same generics.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="keyProjection">Maps a backend document to <typeparamref name="TKey"/>.</param>
    /// <param name="resultProjection">Projects a backend document to the consumer DTO.</param>
    public static IServiceCollection AddGranitIndexingElasticsearchBackend<TKey, TResult>(
        this IServiceCollection services,
        Func<IndexedEntryDocument, TKey> keyProjection,
        Func<IndexedEntryDocument, TResult> resultProjection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(keyProjection);
        ArgumentNullException.ThrowIfNull(resultProjection);

        // Drop any pre-registered ISearchBackend<TKey, TResult> — the host is opting into
        // Elasticsearch, the default EF backend must not also resolve for these generics.
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(ISearchBackend<TKey, TResult>))
            {
                services.RemoveAt(i);
            }
        }

        services.AddScoped<ISearchBackend<TKey, TResult>>(sp => new ElasticsearchSearchBackend<TKey, TResult>(
            sp.GetRequiredService<ElasticsearchClient>(),
            sp.GetRequiredService<IndexingElasticsearchOptions>(),
            sp.GetRequiredService<IndexNameResolver>(),
            sp.GetRequiredService<IndexBootstrapper>(),
            sp.GetRequiredService<MultiTenancy.ICurrentTenant>(),
            keyProjection,
            resultProjection));

        return services;
    }

    /// <summary>
    /// Registers a concrete <see cref="IVectorSearchBackend{TKey, TResult}"/> for the
    /// given <c>(TKey, TResult)</c> tuple, backed by Elasticsearch <c>dense_vector</c>
    /// kNN. Pair with <c>AddGranitIndexingHybridSearch&lt;TKey, TResult&gt;()</c> from
    /// <c>Granit.Indexing.Embeddings</c> to enable hybrid retrieval.
    /// </summary>
    /// <remarks>
    /// MUST be called AFTER <see cref="AddGranitIndexingElasticsearch"/> so the
    /// Elasticsearch client + bootstrapper are in the container. The bootstrapper picks
    /// up <c>GranitIndexingEmbeddingsOptions.Dimensions</c> when present and emits the
    /// <c>dense_vector</c> mapping at first index access.
    /// </remarks>
    public static IServiceCollection AddGranitIndexingElasticsearchVectorBackend<TKey, TResult>(
        this IServiceCollection services,
        Func<IndexedEntryDocument, TKey> keyProjection,
        Func<IndexedEntryDocument, TResult> resultProjection)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(keyProjection);
        ArgumentNullException.ThrowIfNull(resultProjection);

        services.AddScoped<IVectorSearchBackend<TKey, TResult>>(sp => new ElasticsearchVectorSearchBackend<TKey, TResult>(
            sp.GetRequiredService<ElasticsearchClient>(),
            sp.GetRequiredService<IndexNameResolver>(),
            sp.GetRequiredService<IndexBootstrapper>(),
            sp.GetRequiredService<MultiTenancy.ICurrentTenant>(),
            keyProjection,
            resultProjection));

        return services;
    }
}
