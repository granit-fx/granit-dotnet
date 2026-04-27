namespace Granit.Mergeable;

/// <summary>
/// A scalar-field difference between the survivor and the loser of a merge — surfaced in the
/// preview so the admin can pick a winner per field.
/// </summary>
/// <param name="FieldPath">Dot-separated path; e.g. <c>"Name"</c>, <c>"Metadata.segment"</c>.</param>
/// <param name="SurvivorValue">Current value on the surviving aggregate (may be <c>null</c>).</param>
/// <param name="LoserValue">Current value on the loser aggregate (may be <c>null</c>).</param>
/// <param name="Default">Recommended winner — used to pre-tick the admin UI.</param>
public sealed record FieldConflict(
    string FieldPath,
    object? SurvivorValue,
    object? LoserValue,
    WinnerSide Default);
