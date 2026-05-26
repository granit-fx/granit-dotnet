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
        services.TryAddSingleton(sp => new IndexingDbContextSchema(
            indexedKeyTypes,
            sp.GetRequiredService<IndexingEntityFrameworkCoreOptions>().DefaultDictionary));

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
}
