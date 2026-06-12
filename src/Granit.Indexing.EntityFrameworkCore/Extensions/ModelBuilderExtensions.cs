using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Indexing.EntityFrameworkCore.Extensions;

/// <summary>
/// Postgres-scoped extensions for wiring the indexing entity into a DbContext.
/// </summary>
/// <remarks>
/// These helpers live here — NOT in <c>Granit.Persistence.EntityFrameworkCore</c> — because
/// they emit dialect-specific SQL (<c>GENERATED ALWAYS AS … STORED</c>, <c>USING GIN</c>).
/// Keeping the base persistence package provider-neutral is a hard rule.
/// </remarks>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies the Granit Indexing module's entity configurations to a host-owned
    /// <see cref="ModelBuilder"/> — the <c>IndexedEntryRow&lt;TKey&gt;</c> table for each
    /// registered key type, plus the rebuild-job checkpoint table. Use this when the
    /// host wants the indexing entities folded into its consolidated DbContext instead
    /// of running the isolated <c>IndexingDbContext</c>. Both code paths are supported.
    /// </summary>
    /// <param name="modelBuilder">EF Core model builder.</param>
    /// <param name="indexedKeyTypes">
    /// CLR types of the indexed-entry keys (e.g. <c>typeof(Guid)</c>). One table is
    /// created per registered type. Must contain at least one type.
    /// </param>
    /// <param name="defaultDictionary">
    /// Postgres text-search dictionary for the generated <c>tsvector</c> column. Defaults
    /// to <c>english</c> — override per-deployment for other corpora. Ignored on
    /// non-Postgres providers (the column is unmapped).
    /// </param>
    /// <param name="embeddingDimensions">
    /// Vector dimensionality when the host opts into <c>Granit.Indexing.Embeddings</c>.
    /// When <see langword="null"/>, the embedding column is unmapped. On Postgres, the
    /// <c>vector</c> extension is also registered (<c>HasPostgresExtension("vector")</c>).
    /// </param>
    /// <param name="isPostgres">
    /// Whether the host DbContext targets Npgsql. Controls whether the Postgres-specific
    /// generated columns (<c>tsvector</c>, <c>vector(N)</c>) are emitted. Default
    /// <see langword="true"/>; pass <see langword="false"/> for SQLite / in-memory test
    /// rigs where the generated columns can't be translated.
    /// </param>
    /// <remarks>
    /// <para>
    /// Mirrors the pattern in <c>Granit.Auditing.EntityFrameworkCore.ConfigureAuditingModule</c>,
    /// <c>Granit.Presence.EntityFrameworkCore.ConfigurePresenceModule</c>, etc. The
    /// isolated <c>IndexingDbContext</c> calls this internally — single source of truth.
    /// </para>
    /// <para>
    /// <b>Caller responsibilities</b> when using this in a folded host DbContext:
    /// </para>
    /// <list type="bullet">
    /// <item>Add <c>options.UseNpgsql(cs, npg =&gt; npg.UseVector())</c> if embeddings are
    /// active — without it Npgsql throws on the <c>vector</c> column type.</item>
    /// <item>Pair the EF tenant filter wiring (<c>ApplyGranitConventions</c>) as usual.</item>
    /// <item>Migrations live in the host's migration project; the isolated DbContext's
    /// migrations are NOT run when this method is used.</item>
    /// </list>
    /// </remarks>
    public static ModelBuilder ConfigureIndexingModule(
        this ModelBuilder modelBuilder,
        IEnumerable<Type> indexedKeyTypes,
        string defaultDictionary = "english",
        int? embeddingDimensions = null,
        bool isPostgres = true)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(indexedKeyTypes);
        ArgumentException.ThrowIfNullOrEmpty(defaultDictionary);

        Type[] keys = [.. indexedKeyTypes];
        if (keys.Length == 0)
        {
            throw new ArgumentException(
                "At least one TKey must be supplied — Granit.Indexing maps one table per key type.",
                nameof(indexedKeyTypes));
        }

        if (isPostgres && embeddingDimensions.HasValue)
        {
            modelBuilder.HasPostgresExtension("vector");
        }

        modelBuilder.ApplyConfiguration(new Configurations.IndexingRebuildCheckpointRowConfiguration());

        foreach (Type keyType in keys)
        {
            ApplyConfigurationMethod
                .MakeGenericMethod(typeof(IndexedEntryRow<>).MakeGenericType(keyType))
                .Invoke(modelBuilder, [Activator.CreateInstance(typeof(Configurations.IndexedEntryRowConfiguration<>).MakeGenericType(keyType))!]);

            if (isPostgres)
            {
                HasGeneratedTsVectorColumnMethod
                    .MakeGenericMethod(keyType)
                    .Invoke(null, [modelBuilder, defaultDictionary]);

                if (embeddingDimensions.HasValue)
                {
                    HasEmbeddingColumnMethod
                        .MakeGenericMethod(keyType)
                        .Invoke(null, [modelBuilder, embeddingDimensions.Value]);
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
                IgnoreSearchVectorMethod
                    .MakeGenericMethod(keyType)
                    .Invoke(null, [modelBuilder]);
                IgnoreEmbeddingColumnMethod
                    .MakeGenericMethod(keyType)
                    .Invoke(null, [modelBuilder]);
            }
        }

        return modelBuilder;
    }

    private static readonly MethodInfo ApplyConfigurationMethod = typeof(ModelBuilder)
        .GetMethods()
        .Single(m => m.Name == nameof(ModelBuilder.ApplyConfiguration) && m.IsGenericMethod && m.GetParameters().Length == 1);

    private static readonly MethodInfo HasGeneratedTsVectorColumnMethod =
        typeof(ModelBuilderExtensions).GetMethod(
            nameof(HasGeneratedTsVectorColumn),
            BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo HasEmbeddingColumnMethod =
        typeof(ModelBuilderExtensions).GetMethod(
            nameof(HasEmbeddingColumn),
            BindingFlags.Static | BindingFlags.Public)!;

    private static readonly MethodInfo IgnoreEmbeddingColumnMethod =
        typeof(ModelBuilderExtensions).GetMethod(
            nameof(IgnoreEmbeddingColumn),
            BindingFlags.Static | BindingFlags.Public)!;

    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "Reflects over this type's OWN internal IgnoreSearchVector<TKey> to close it over a key type " +
            "known only at runtime; it does not widen another type's encapsulation. The helper is kept non-public to " +
            "stay out of the public API while remaining reflectively dispatchable per registered key type.")]
    private static readonly MethodInfo IgnoreSearchVectorMethod =
        typeof(ModelBuilderExtensions).GetMethod(
            nameof(IgnoreSearchVector),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    internal static EntityTypeBuilder<IndexedEntryRow<TKey>> IgnoreSearchVector<TKey>(
        this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        EntityTypeBuilder<IndexedEntryRow<TKey>> entity = modelBuilder.Entity<IndexedEntryRow<TKey>>();
        entity.Ignore(e => e.SearchVector);
        return entity;
    }

    /// <summary>
    /// Configures <see cref="IndexedEntryRow{TKey}.SearchVector"/> as a Postgres
    /// <c>tsvector</c> column generated from <see cref="IndexedEntryRow{TKey}.Content"/>
    /// using the dictionary identified by <see cref="IndexedEntryRow{TKey}.Language"/>
    /// (falling back to <c>simple</c> when null/unknown), then adds a GIN index on it.
    /// </summary>
    /// <param name="modelBuilder">EF Core model builder.</param>
    /// <param name="defaultDictionary">
    /// Postgres text-search dictionary used when <c>Language</c> is null. Defaults to
    /// <c>simple</c> (language-agnostic, no stemming or stop-words) so the column never
    /// fails to build on un-recognised languages. Application hosts can override per
    /// deployment (e.g. <c>english</c> for English-only corpora).
    /// </param>
    /// <typeparam name="TKey">Resource primary key.</typeparam>
    public static EntityTypeBuilder<IndexedEntryRow<TKey>> HasGeneratedTsVectorColumn<TKey>(
        this ModelBuilder modelBuilder,
        string defaultDictionary = "simple")
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentException.ThrowIfNullOrEmpty(defaultDictionary);

        EntityTypeBuilder<IndexedEntryRow<TKey>> entity = modelBuilder.Entity<IndexedEntryRow<TKey>>();

        // STORED generated column. Postgres requires the generation expression to be
        // IMMUTABLE — `to_tsvector(regconfig, text)` with a per-row regconfig fails that
        // check (the cast `text::regconfig` is STABLE only). The pragmatic Postgres-native
        // pattern is to fix the dictionary at the table level and use the `to_tsvector(text)`
        // overload with the configured default. Per-row `Language` is still stored for
        // query-time analysis selection (the search backend picks the matching dictionary
        // for the query); content stemming is uniform within a table.
        //
        // Multilingual corpora that need per-language stemming at index time should adopt
        // per-language partitions or columns — out of scope for the default backend.
        string fallback = SanitiseDictionary(defaultDictionary);
        string expression = $"to_tsvector('{fallback}'::regconfig, coalesce(\"Content\", ''))";

        entity.Property(e => e.SearchVector)
            .HasColumnType("tsvector")
            .HasComputedColumnSql(expression, stored: true);

        entity.HasIndex(e => e.SearchVector)
            .HasMethod("GIN")
            .HasDatabaseName($"ix_{GranitIndexingDbProperties.DbTablePrefix}indexed_entry_{typeof(TKey).Name.ToLowerInvariant()}_search_vector_gin");

        return entity;
    }

    /// <summary>
    /// Configures <see cref="IndexedEntryRow{TKey}.Embedding"/> as a Postgres
    /// <c>vector(N)</c> column with an HNSW index using <c>vector_cosine_ops</c>.
    /// Idempotent — calling twice for the same key type is a no-op past the first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>VULN-201 atomic Art. 17 delete.</b> The embedding column lives on the same
    /// row as <see cref="IndexedEntryRow{TKey}.Content"/> so the existing
    /// <c>IIndexedDataEraser</c> cascade purges both atoms in a single statement.
    /// </para>
    /// <para>
    /// <b>HNSW caveat.</b> Postgres' HNSW index retains stale graph pointers after
    /// <c>DELETE</c> until a <c>REINDEX INDEX CONCURRENTLY</c> runs. Operators MUST
    /// schedule this for full GDPR Art. 17 conformance — see
    /// <c>granit-docs</c> "Embeddings + GDPR Art. 17" page for a <c>pg_cron</c> snippet.
    /// </para>
    /// </remarks>
    public static EntityTypeBuilder<IndexedEntryRow<TKey>> HasEmbeddingColumn<TKey>(
        this ModelBuilder modelBuilder,
        int dimensions)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);

        EntityTypeBuilder<IndexedEntryRow<TKey>> entity = modelBuilder.Entity<IndexedEntryRow<TKey>>();

        entity.Property(e => e.Embedding)
            .HasColumnType($"vector({dimensions})");

        entity.HasIndex(e => e.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops")
            .HasDatabaseName($"ix_{GranitIndexingDbProperties.DbTablePrefix}indexed_entry_{typeof(TKey).Name.ToLowerInvariant()}_embedding_hnsw");

        return entity;
    }

    /// <summary>
    /// Unmaps <see cref="IndexedEntryRow{TKey}.Embedding"/> from the model — used by
    /// the DbContext when the host does NOT opt into embeddings, so the EF model never
    /// tries to project a <c>vector</c> column that isn't there.
    /// </summary>
    public static EntityTypeBuilder<IndexedEntryRow<TKey>> IgnoreEmbeddingColumn<TKey>(
        this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        EntityTypeBuilder<IndexedEntryRow<TKey>> entity = modelBuilder.Entity<IndexedEntryRow<TKey>>();
        entity.Ignore(e => e.Embedding);
        return entity;
    }

    private static string SanitiseDictionary(string raw)
    {
        foreach (char c in raw)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                throw new ArgumentException(
                    $"Invalid Postgres dictionary identifier '{raw}'. Only letters, digits and underscores are allowed.",
                    nameof(raw));
            }
        }

        return raw;
    }
}
