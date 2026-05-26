using Granit.Indexing.BackgroundJobs;
using Granit.Indexing.Embeddings;
using Granit.Indexing.Embeddings.Options;
using Granit.Indexing.EntityFrameworkCore.Internal;
using Granit.Indexing.EntityFrameworkCore.Options;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Indexing.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Indexing.EntityFrameworkCore</c> services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF-backed indexing storage: the
    /// <see cref="IndexingDbContext"/> factory, the indexer, the search backend factory,
    /// the GDPR Art. 17 cascade handler, and runtime options.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configureDbContext">
    /// Per-host DbContext configuration delegate — typically
    /// <c>opts =&gt; opts.UseNpgsql(connectionString)</c>. The framework intentionally
    /// does NOT pick a provider for the host.
    /// </param>
    /// <param name="indexedKeyTypes">
    /// The CLR types of keys this host indexes (e.g. <c>typeof(Guid)</c>). One physical
    /// table is created per registered type.
    /// </param>
    public static IServiceCollection AddGranitIndexingEntityFrameworkCore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext,
        params Type[] indexedKeyTypes)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);
        ArgumentNullException.ThrowIfNull(indexedKeyTypes);

        if (indexedKeyTypes.Length == 0)
        {
            throw new ArgumentException(
                "At least one TKey must be registered for the indexing DbContext. " +
                "Typical default is typeof(Guid).",
                nameof(indexedKeyTypes));
        }

        services.AddOptions<IndexingEntityFrameworkCoreOptions>()
            .BindConfiguration(IndexingEntityFrameworkCoreOptions.SectionName);

        services.TryAddSingleton(sp =>
            sp.GetRequiredService<IOptions<IndexingEntityFrameworkCoreOptions>>().Value);

        // Schema bundle captured once at registration; the DbContext reads it from DI.
        // EmbeddingDimensions is folded in lazily so a host that calls
        // AddGranitIndexingEmbeddings() AFTER this extension still gets a vector-aware
        // model — as long as it happens before the first DbContext use.
        services.TryAddSingleton(sp => new IndexingDbContextSchema(
            indexedKeyTypes,
            sp.GetRequiredService<IndexingEntityFrameworkCoreOptions>().DefaultDictionary,
            sp.GetService<IOptions<GranitIndexingEmbeddingsOptions>>()?.Value.Dimensions is int d and > 0 ? d : null));

        services.AddDbContextFactory<IndexingDbContext>((sp, options) =>
        {
            configureDbContext(options);
            options.UseGranitInterceptors(sp);
        }, ServiceLifetime.Scoped);

        // One IIndexer<TKey> per registered TKey.
        foreach (Type keyType in indexedKeyTypes)
        {
            Type indexerService = typeof(IIndexer<>).MakeGenericType(keyType);
            Type indexerImpl = typeof(EfIndexer<>).MakeGenericType(keyType);
            services.TryAdd(ServiceDescriptor.Scoped(indexerService, indexerImpl));
        }

        // Single GDPR Art. 17 eraser scans every registered TKey. Registered as a plain
        // Add (not TryAdd) so multiple backends compose — the privacy bridge calls every
        // eraser sequentially.
        services.AddScoped<IIndexedDataEraser, EfIndexedDataEraser>();

        return services;
    }

    /// <summary>
    /// Registers a concrete <c>ISearchBackend&lt;TKey, TResult&gt;</c> for the given
    /// projection. Consumers call this once per (TKey, TResult) tuple they search on —
    /// the projection runs server-side (LINQ expression) so non-content columns can be
    /// stripped before the row is materialised.
    /// </summary>
    public static IServiceCollection AddGranitIndexingBackend<TKey, TResult>(
        this IServiceCollection services,
        Func<IndexedEntryRow<TKey>, TResult> projection)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(projection);

        services.TryAddScoped<ISearchBackend<TKey, TResult>>(sp => new EfSearchBackend<TKey, TResult>(
            sp.GetRequiredService<IDbContextFactory<IndexingDbContext>>(),
            sp.GetRequiredService<IndexingEntityFrameworkCoreOptions>(),
            projection));

        return services;
    }

    /// <summary>
    /// Registers a concrete <see cref="IVectorSearchBackend{TKey, TResult}"/> for the
    /// given projection, backed by pgvector cosine kNN. Pair with
    /// <c>AddGranitIndexingHybridSearch&lt;TKey, TResult&gt;()</c> from
    /// <c>Granit.Indexing.Embeddings</c> to enable hybrid retrieval.
    /// </summary>
    /// <remarks>
    /// <para>
    /// MUST be called AFTER <see cref="AddGranitIndexingEntityFrameworkCore"/> so the
    /// <see cref="IDbContextFactory{IndexingDbContext}"/> is in the container, and MUST
    /// be paired with an <c>AddGranitIndexingEmbeddings()</c> call that binds
    /// <see cref="GranitIndexingEmbeddingsOptions.Dimensions"/> so the DbContext model
    /// maps the <c>vector(N)</c> column at build time.
    /// </para>
    /// <para>
    /// Hosts must also enable the pgvector type mapping in their Npgsql configuration:
    /// <c>opts.UseNpgsql(cs, npg =&gt; npg.UseVector())</c>. Without it, EF Core throws
    /// at first index attempt.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGranitIndexingEmbeddingsBackend<TKey, TResult>(
        this IServiceCollection services,
        Func<IndexedEntryRow<TKey>, TResult> projection)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(projection);

        services.TryAddScoped<IVectorSearchBackend<TKey, TResult>>(sp => new EfVectorSearchBackend<TKey, TResult>(
            sp.GetRequiredService<IDbContextFactory<IndexingDbContext>>(),
            projection));

        return services;
    }

    /// <summary>
    /// Replaces the in-memory <see cref="IRebuildCheckpointStore{TKey}"/> default with
    /// a persistent EF-backed implementation rooted in <see cref="IndexingDbContext"/>.
    /// Required for production rebuilds — the in-memory default loses state on worker
    /// restart.
    /// </summary>
    /// <remarks>
    /// MUST be called AFTER <see cref="AddGranitIndexingEntityFrameworkCore"/> AND after
    /// the host has called <c>AddGranitIndexingBackgroundJobs()</c> so the in-memory
    /// fallback is already registered and can be replaced cleanly via TryAdd-then-replace.
    /// </remarks>
    public static IServiceCollection AddGranitIndexingEntityFrameworkCoreCheckpointStore<TKey>(
        this IServiceCollection services)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(services);

        // Strip any pre-registered IRebuildCheckpointStore<TKey> — most likely the
        // in-memory fallback from AddGranitIndexingBackgroundJobs(). The host explicitly
        // opted into persistent checkpoints, the default must not also resolve.
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(IRebuildCheckpointStore<TKey>))
            {
                services.RemoveAt(i);
            }
        }

        services.AddScoped<IRebuildCheckpointStore<TKey>>(sp => new EfRebuildCheckpointStore<TKey>(
            sp.GetRequiredService<IDbContextFactory<IndexingDbContext>>(),
            sp.GetRequiredService<TimeProvider>()));

        return services;
    }
}
