namespace Granit.Entities.Customization.Domain.Deltas;

/// <summary>
/// Drops <paramref name="FieldName"/> from the rendered layout. The compiled
/// definition's permission gate is unchanged — Layer 1 cannot grant or revoke
/// access (ADR-053 §validation rules).
/// </summary>
public sealed record HideDelta(string FieldName) : LayoutDelta(FieldName);
