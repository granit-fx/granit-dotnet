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
