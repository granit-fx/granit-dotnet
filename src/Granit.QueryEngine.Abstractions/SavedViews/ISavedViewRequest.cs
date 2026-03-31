namespace Granit.QueryEngine.SavedViews;

/// <summary>
/// Shared contract for saved view Create and Update request payloads.
/// Enables reusable FluentValidation rules across both operations.
/// </summary>
public interface ISavedViewRequest
{
    /// <summary>User-facing name of the saved view.</summary>
    string Name { get; }

    /// <summary>Serialized filter criteria (JSON), or <c>null</c>.</summary>
    string? FilterJson { get; }

    /// <summary>Serialized sort specification (JSON), or <c>null</c>.</summary>
    string? SortJson { get; }

    /// <summary>Serialized group-by specification (JSON), or <c>null</c>.</summary>
    string? GroupByJson { get; }

    /// <summary>Serialized visible column list (JSON), or <c>null</c>.</summary>
    string? VisibleColumnsJson { get; }
}
