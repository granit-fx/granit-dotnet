namespace Granit.Entities.Customization.Domain.Deltas;

/// <summary>
/// Moves <paramref name="FieldName"/> into the group identified by
/// <paramref name="GroupKey"/>. The group key MUST resolve in the compiled
/// definition's group catalogue.
/// </summary>
public sealed record RegroupDelta(
    string FieldName,
    string GroupKey) : LayoutDelta(FieldName);
