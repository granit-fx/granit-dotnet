namespace Granit.QueryEngine.Options;

/// <summary>
/// Global configuration for query definitions. Values set here are used as defaults
/// for all definitions that do not explicitly call <c>.DefaultPageSize()</c> or
/// <c>.MaxPageSize()</c> in their builder.
/// </summary>
/// <remarks>
/// Bind from configuration:
/// <code>
/// services.Configure&lt;QueryEngineOptions&gt;(configuration.GetSection(QueryEngineOptions.SectionName));
/// </code>
/// Or configure inline:
/// <code>
/// services.Configure&lt;QueryEngineOptions&gt;(o =&gt; o.DefaultPageSize = 50);
/// </code>
/// </remarks>
public sealed class QueryEngineOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "QueryEngine";

    /// <summary>Default page size for paginated queries. Default: <c>20</c>.</summary>
    public int DefaultPageSize { get; set; } = QueryEngineDefaults.DefaultPageSize;

    /// <summary>Maximum allowed page size. Default: <c>100</c>.</summary>
    public int MaxPageSize { get; set; } = QueryEngineDefaults.MaxPageSize;

    /// <summary>Maximum number of items returned by streaming queries. Default: <c>100_000</c>.</summary>
    public int MaxStreamSize { get; set; } = QueryEngineDefaults.MaxStreamSize;

    /// <summary>
    /// Maximum number of groups returned by grouped queries. Default: <c>1000</c>.
    /// Prevents memory exhaustion from high-cardinality group-by fields.
    /// </summary>
    public int MaxGroupCount { get; set; } = 1000;

    /// <summary>
    /// Base64-encoded HMAC-SHA256 key for signing cursor tokens (CWE-565 mitigation).
    /// When <c>null</c>, cursors are unsigned (backward compatible). Configure a 256-bit
    /// (32-byte) key for production use.
    /// </summary>
    public string? CursorHmacKey { get; set; }
}
