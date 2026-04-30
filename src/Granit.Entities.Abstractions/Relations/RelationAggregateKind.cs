namespace Granit.Entities.Relations;

/// <summary>
/// Closed enum of aggregates a relation may surface on the source entity's
/// detail header (smart-button counts, sidebar metrics, …).
/// </summary>
public enum RelationAggregateKind
{
    /// <summary>Count of related rows.</summary>
    Count,

    /// <summary>Sum over a numeric property.</summary>
    Sum,

    /// <summary>Average over a numeric property.</summary>
    Avg,

    /// <summary>Min over a numeric property.</summary>
    Min,

    /// <summary>Max over a numeric property.</summary>
    Max,
}
