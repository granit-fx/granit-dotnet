namespace Granit.QueryEngine.SavedViews;

/// <summary>
/// Request payload for creating a new saved view.
/// </summary>
public sealed record CreateSavedViewRequest
{
    /// <summary>User-facing name of the saved view.</summary>
    public required string Name { get; init; }

    /// <summary>Whether this view is shared with other users.</summary>
    public bool IsShared { get; init; }

    /// <summary>Whether this is the user's default view.</summary>
    public bool IsDefault { get; init; }

    /// <summary>Serialized filter criteria (JSON), or <c>null</c>.</summary>
    public string? FilterJson { get; init; }

    /// <summary>Serialized sort specification (JSON), or <c>null</c>.</summary>
    public string? SortJson { get; init; }

    /// <summary>Serialized group-by specification (JSON), or <c>null</c>.</summary>
    public string? GroupByJson { get; init; }

    /// <summary>Serialized visible column list (JSON), or <c>null</c>.</summary>
    public string? VisibleColumnsJson { get; init; }
}
