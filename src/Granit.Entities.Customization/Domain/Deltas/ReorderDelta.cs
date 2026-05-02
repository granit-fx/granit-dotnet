namespace Granit.Entities.Customization.Domain.Deltas;

/// <summary>
/// Moves <paramref name="FieldName"/> to a new position within its current
/// group. Exactly one of <paramref name="BeforeFieldName"/> /
/// <paramref name="AfterFieldName"/> MUST be set; both null or both set is
/// rejected at the endpoint boundary (ADR-053 §validation rules).
/// </summary>
public sealed record ReorderDelta(
    string FieldName,
    string? BeforeFieldName,
    string? AfterFieldName) : LayoutDelta(FieldName)
{
    /// <summary>True when the (Before, After) pair is well-formed (exactly one set).</summary>
    public bool IsAnchorWellFormed =>
        (BeforeFieldName is not null) ^ (AfterFieldName is not null);
}
