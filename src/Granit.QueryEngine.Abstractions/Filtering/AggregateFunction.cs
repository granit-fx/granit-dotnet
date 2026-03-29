namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Aggregate functions available for grouped queries.
/// </summary>
public enum AggregateFunction
{
    /// <summary>Count of items.</summary>
    Count,

    /// <summary>Sum of values.</summary>
    Sum,

    /// <summary>Average of values.</summary>
    Avg,

    /// <summary>Minimum value.</summary>
    Min,

    /// <summary>Maximum value.</summary>
    Max,
}
