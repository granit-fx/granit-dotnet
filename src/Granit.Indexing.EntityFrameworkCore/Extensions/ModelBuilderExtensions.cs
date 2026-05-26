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
            .HasDatabaseName($"IX_IndexedEntry_{typeof(TKey).Name}_SearchVector_GIN");

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
            .HasDatabaseName($"IX_IndexedEntry_{typeof(TKey).Name}_Embedding_HNSW");

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
