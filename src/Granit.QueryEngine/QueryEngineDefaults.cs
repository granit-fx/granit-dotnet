namespace Granit.QueryEngine;

/// <summary>
/// Default values for query definitions.
/// </summary>
public static class QueryEngineDefaults
{
    /// <summary>Default page size for paginated queries.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Maximum allowed page size.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Maximum number of items returned by <c>ExecuteStreamAsync</c>.</summary>
    public const int MaxStreamSize = 100_000;
}
