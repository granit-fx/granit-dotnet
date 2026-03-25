namespace Granit.QueryEngine.Options;

/// <summary>
/// Global configuration for query definitions. Values set here are used as defaults
/// for all definitions that do not explicitly call <c>.DefaultPageSize()</c> or
/// <c>.MaxPageSize()</c> in their builder.
/// </summary>
/// <remarks>
/// Bind from configuration:
/// <code>
/// services.Configure&lt;QueryEngineOptions&gt;(configuration.GetSection("QueryEngine"));
/// </code>
/// Or configure inline:
/// <code>
/// services.Configure&lt;QueryEngineOptions&gt;(o =&gt; o.DefaultPageSize = 50);
/// </code>
/// </remarks>
public sealed class QueryEngineOptions
{
    /// <summary>Default page size for paginated queries. Default: <c>20</c>.</summary>
    public int DefaultPageSize { get; set; } = QueryEngineDefaults.DefaultPageSize;

    /// <summary>Maximum allowed page size. Default: <c>100</c>.</summary>
    public int MaxPageSize { get; set; } = QueryEngineDefaults.MaxPageSize;

    /// <summary>Maximum number of items returned by streaming queries. Default: <c>100_000</c>.</summary>
    public int MaxStreamSize { get; set; } = QueryEngineDefaults.MaxStreamSize;
}
