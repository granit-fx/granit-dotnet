namespace Granit.QueryEngine.Endpoints.Dtos;

/// <summary>
/// Response DTO for a saved view entry.
/// </summary>
public sealed record SavedViewResponse(
    Guid Id,
    string EntityType,
    string Name,
    string UserId,
    bool IsShared,
    bool IsDefault,
    string? FilterJson,
    string? SortJson,
    string? GroupByJson,
    string? VisibleColumnsJson);
