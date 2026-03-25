namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Filter operators available for query filters.
/// </summary>
public enum FilterOperator
{
    /// <summary>Equal (<c>==</c>).</summary>
    Eq,

    /// <summary>Contains (string <c>LIKE %value%</c>).</summary>
    Contains,

    /// <summary>Starts with (string <c>LIKE value%</c>).</summary>
    StartsWith,

    /// <summary>Ends with (string <c>LIKE %value</c>).</summary>
    EndsWith,

    /// <summary>Greater than (<c>&gt;</c>).</summary>
    Gt,

    /// <summary>Greater than or equal (<c>&gt;=</c>).</summary>
    Gte,

    /// <summary>Less than (<c>&lt;</c>).</summary>
    Lt,

    /// <summary>Less than or equal (<c>&lt;=</c>).</summary>
    Lte,

    /// <summary>Value is in a set (<c>IN (...)</c>).</summary>
    In,

    /// <summary>Value is between two bounds (inclusive).</summary>
    Between,
}
