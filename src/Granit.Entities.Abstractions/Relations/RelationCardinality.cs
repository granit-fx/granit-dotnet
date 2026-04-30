namespace Granit.Entities.Relations;

/// <summary>
/// Cardinality of an entity relation declared via <c>HasMany&lt;T&gt;</c> /
/// <c>HasOne&lt;T&gt;</c>.
/// </summary>
public enum RelationCardinality
{
    /// <summary>1:N — the source has zero or more related rows.</summary>
    Many,

    /// <summary>1:1 — the source has at most one related row.</summary>
    One,
}
