using System.Reflection;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore;

/// <summary>
/// Isolated EF Core DbContext for the indexing module. One physical
/// <see cref="DbSet{TEntity}"/> per registered key type.
/// </summary>
/// <remarks>
/// <para>
/// <b>Multi-tenant.</b> Inherits <see cref="GranitDbContext"/> so the tenant query
/// filter is parameterised at the SQL level (no closure-captured constant leak).
/// </para>
/// <para>
/// <b>Per-key-type shape.</b> Consumers pass every key type they index to
/// <c>AddGranitIndexingEntityFrameworkCore</c>; the DbContext enumerates registrations
/// from <see cref="IndexingDbContextSchema"/> at <see cref="OnGranitModelCreating"/>
/// time and maps one <see cref="IndexedEntryRow{TKey}"/> per registered key type.
/// </para>
/// </remarks>
public sealed class IndexingDbContext : GranitDbContext
{
    internal IReadOnlyList<Type> IndexedKeyTypes { get; }
    internal string DefaultDictionary { get; }
    internal int? EmbeddingDimensions { get; }

    public IndexingDbContext(
        DbContextOptions<IndexingDbContext> options,
        ICurrentTenant currentTenant,
        IndexingDbContextSchema schema,
        IDataFilter? dataFilter = null)
        : base(options, currentTenant, dataFilter)
    {
        ArgumentNullException.ThrowIfNull(schema);
        IndexedKeyTypes = schema.KeyTypes;
        DefaultDictionary = schema.DefaultDictionary;
        EmbeddingDimensions = schema.EmbeddingDimensions;
    }

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        bool isPostgres = string.Equals(Database.ProviderName, GranitDbProviders.Postgres, StringComparison.Ordinal);

        if (isPostgres && EmbeddingDimensions.HasValue)
        {
            modelBuilder.HasPostgresExtension("vector");
        }

        foreach (Type keyType in IndexedKeyTypes)
        {
            ApplyConfigurationMethod
                .MakeGenericMethod(typeof(IndexedEntryRow<>).MakeGenericType(keyType))
                .Invoke(modelBuilder, [Activator.CreateInstance(typeof(Configurations.IndexedEntryRowConfiguration<>).MakeGenericType(keyType))!]);

            if (isPostgres)
            {
                HasGeneratedTsVectorColumnMethod
                    .MakeGenericMethod(keyType)
                    .Invoke(null, [modelBuilder, DefaultDictionary]);

                if (EmbeddingDimensions.HasValue)
                {
                    HasEmbeddingColumnMethod
                        .MakeGenericMethod(keyType)
                        .Invoke(null, [modelBuilder, EmbeddingDimensions.Value]);
                }
                else
                {
                    IgnoreEmbeddingColumnMethod
                        .MakeGenericMethod(keyType)
                        .Invoke(null, [modelBuilder]);
                }
            }
            else
            {
                // Non-Postgres providers (in-memory / SQLite test rigs) don't have tsvector
                // or pgvector; unmap both so EF Core's relational provider doesn't try to
                // emit columns it cannot translate.
                IgnoreSearchVectorMethod
                    .MakeGenericMethod(keyType)
                    .Invoke(null, [modelBuilder]);
                IgnoreEmbeddingColumnMethod
                    .MakeGenericMethod(keyType)
                    .Invoke(null, [modelBuilder]);
            }
        }

        modelBuilder.ApplyGranitConventions(currentTenant: null, dataFilter: null);
    }

    private static readonly MethodInfo ApplyConfigurationMethod = typeof(ModelBuilder)
        .GetMethods()
        .Single(m => m.Name == nameof(ModelBuilder.ApplyConfiguration) && m.IsGenericMethod && m.GetParameters().Length == 1);

    private static readonly MethodInfo HasGeneratedTsVectorColumnMethod =
        typeof(Extensions.ModelBuilderExtensions).GetMethod(
            nameof(Extensions.ModelBuilderExtensions.HasGeneratedTsVectorColumn),
            BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo HasEmbeddingColumnMethod =
        typeof(Extensions.ModelBuilderExtensions).GetMethod(
            nameof(Extensions.ModelBuilderExtensions.HasEmbeddingColumn),
            BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo IgnoreEmbeddingColumnMethod =
        typeof(Extensions.ModelBuilderExtensions).GetMethod(
            nameof(Extensions.ModelBuilderExtensions.IgnoreEmbeddingColumn),
            BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo IgnoreSearchVectorMethod =
        typeof(IndexingDbContext).GetMethod(
            nameof(IgnoreSearchVector),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static void IgnoreSearchVector<TKey>(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<IndexedEntryRow<TKey>>().Ignore(e => e.SearchVector);
}

/// <summary>
/// Per-DbContext schema bundle: which TKey tables to map, which fallback dictionary to
/// use for tsvector generation, and (optionally) the embedding vector dimensionality
/// when the host opts into <c>Granit.Indexing.Embeddings</c>. Resolved from DI by
/// <see cref="IndexingDbContext"/> at construction time.
/// </summary>
public sealed record IndexingDbContextSchema(
    IReadOnlyList<Type> KeyTypes,
    string DefaultDictionary,
    int? EmbeddingDimensions = null);
