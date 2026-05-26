namespace Granit.Indexing.Elasticsearch.Options;

/// <summary>
/// Configuration options for <c>Granit.Indexing.Elasticsearch</c>. Bound from the
/// <see cref="SectionName"/> section of <c>appsettings.json</c>.
/// </summary>
public sealed class IndexingElasticsearchOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Indexing:Elasticsearch";

    /// <summary>
    /// Cluster endpoint URI (e.g. <c>https://es.internal:9200</c>). Required.
    /// </summary>
    public string Uri { get; set; } = string.Empty;

    /// <summary>
    /// Elastic Cloud / cluster API key. <c>null</c> for unauthenticated clusters.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>Index layout strategy. Default: <see cref="ElasticsearchTenancyStrategy.Shared"/>.</summary>
    public ElasticsearchTenancyStrategy Strategy { get; set; } = ElasticsearchTenancyStrategy.Shared;

    /// <summary>
    /// Prefix prepended to every index name created by this module. The final index name
    /// is <c>{IndexPrefix}-{tkey}[-{tenantId}]</c>. Default: <c>granit-indexing</c>.
    /// </summary>
    public string IndexPrefix { get; set; } = "granit-indexing";

    /// <summary>
    /// Batch size used when a future Wolverine sync handler bulk-streams entries from the
    /// EF backend into ES (I-F2.2 follow-up). Default: <c>500</c>.
    /// </summary>
    public int BulkBatchSize { get; set; } = 500;

    /// <summary>
    /// When <c>true</c>, the document persists the full <see cref="IndexedEntry{TKey}.Content"/>
    /// in the ES index for snippet generation and reindexing. When <c>false</c>, ES stores
    /// only the analyzed term vector plus the resource key; snippets must be re-extracted
    /// on demand via <c>Granit.TextExtraction</c>. Default: <c>true</c>.
    /// </summary>
    public bool StoreFullContentInIndex { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, the search backend uses <c>simple_query_string</c> with a
    /// restricted operator set (<c>AND | OR | PREFIX | PHRASE</c>) — supports quoted
    /// phrases, prefix wildcards, and boolean operators while still rejecting Lucene's
    /// dangerous regex/fuzzy/range syntax. When <c>false</c>, every query is treated as
    /// a single multi-field <c>match</c> phrase (every token AND-ed). Default: <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Full <c>query_string</c> (with regex, fuzzy and field-targeted operators) is the
    /// historic Lucene injection vector and is intentionally not reachable from this
    /// path. Hosts that need it gate a separate endpoint behind a
    /// <c>Search.Advanced.Execute</c> permission and forward <see cref="SearchRequest.UseAdvancedSyntax"/>
    /// after the check.
    /// </remarks>
    public bool UseSimpleQueryString { get; set; } = true;

    /// <summary>
    /// Per-language analyzers applied to the <c>content</c> field. Keys are ISO 639-1
    /// codes; values are Elasticsearch built-in analyzer names. Unknown codes fall back to
    /// <see cref="DefaultAnalyzer"/>. Defaults cover the 18 cultures shipped by Granit.
    /// </summary>
    public IDictionary<string, string> LanguageAnalyzers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "english",
        ["fr"] = "french",
        ["nl"] = "dutch",
        ["de"] = "german",
        ["es"] = "spanish",
        ["it"] = "italian",
        ["pt"] = "portuguese",
        ["zh"] = "cjk",
        ["ja"] = "cjk",
        ["ko"] = "cjk",
        ["pl"] = "polish",
        ["tr"] = "turkish",
        ["sv"] = "swedish",
        ["cs"] = "czech",
        ["hi"] = "hindi",
    };

    /// <summary>
    /// Analyzer used when an entry's language is <c>null</c> or unmapped. Default:
    /// <c>standard</c> (Elasticsearch's language-agnostic default).
    /// </summary>
    public string DefaultAnalyzer { get; set; } = "standard";
}
