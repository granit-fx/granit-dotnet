namespace Granit.EntityMerge;

/// <summary>Which side of a merge wins for a given field — chosen by the admin in the UI.</summary>
public enum WinnerSide
{
    /// <summary>Keep the survivor's value (default for most fields).</summary>
    Survivor,

    /// <summary>Replace the survivor's value with the loser's.</summary>
    Loser,
}
