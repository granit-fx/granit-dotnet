namespace Granit.Indexing.EntityFrameworkCore.Options;

/// <summary>
/// Configuration options for <c>Granit.Indexing.EntityFrameworkCore</c>. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class IndexingEntityFrameworkCoreOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Indexing:EntityFrameworkCore";

    /// <summary>
    /// Postgres text-search dictionary used when an entry's
    /// <see cref="IndexedEntryRow{TKey}.Language"/> is <c>null</c>. Defaults to
    /// <c>simple</c> (language-agnostic, no stemming) so the generated column never fails
    /// to build. Override to <c>english</c>, <c>french</c>, etc. for mono-lingual corpora.
    /// </summary>
    public string DefaultDictionary { get; set; } = "simple";

    /// <summary>
    /// When <c>true</c>, the search backend uses Postgres' <c>websearch_to_tsquery</c>
    /// (Postgres 11+) for the default search path — it understands quoted phrases and
    /// <c>OR</c>/<c>-</c> like a web search engine while still treating unknown
    /// operators as literals. When <c>false</c>, falls back to <c>plainto_tsquery</c>
    /// (every token AND-ed). Both forms ignore raw tsquery operator syntax, so neither
    /// re-opens the tsquery-injection surface.
    /// </summary>
    public bool UseWebSearchSyntax { get; set; } = true;
}
