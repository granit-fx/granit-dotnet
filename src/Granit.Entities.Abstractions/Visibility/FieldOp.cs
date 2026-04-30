namespace Granit.Entities.Visibility;

/// <summary>
/// Closed enum of operators allowed in the visibility DSL (per ADR-040 / ADR-044).
/// Deliberately small — sufficient for 95% of conditional visibility cases without
/// inviting the eval-string Pandora's box documented in PR #1599 conversation.
/// </summary>
public enum FieldOp
{
    /// <summary>Equality test against a single literal value.</summary>
    Eq,

    /// <summary>Inequality test against a single literal value.</summary>
    NotEq,

    /// <summary>Membership test against a finite set of literal values.</summary>
    In,

    /// <summary>Negated membership test against a finite set of literal values.</summary>
    NotIn,

    /// <summary>Strictly-greater-than comparison against a single literal value.</summary>
    Gt,

    /// <summary>Strictly-less-than comparison against a single literal value.</summary>
    Lt,

    /// <summary>True when the field's current value is null or unset.</summary>
    IsNull,

    /// <summary>True when the field's current value is non-null.</summary>
    IsNotNull,
}
