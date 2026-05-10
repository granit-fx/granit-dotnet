namespace Granit.Entities.Endpoints.Dtos.BulkActions;

/// <summary>
/// Server response for a bulk action invocation.  Includes the count of successfully
/// affected rows and a list of per-row failures (if any). Allows clients to distinguish
/// partial success from total failure and retry specific rows.
/// </summary>
public sealed record BulkActionResponse(
    /// <summary>
    /// Count of rows successfully affected by the action. Rows that failed are
    /// not included in this count.
    /// </summary>
    int Affected,
    /// <summary>
    /// Per-row failure details. When empty, all requested rows succeeded
    /// (<see cref="Affected"/> equals request.Ids.Count).
    /// </summary>
    IReadOnlyList<BulkActionFailureResponse> Failures);

/// <summary>
/// Details of a single row's failure in a bulk operation. Clients use this to
/// surface error feedback per entity in their UI (e.g., a toast or inline message).
/// </summary>
public sealed record BulkActionFailureResponse(
    /// <summary>
    /// Stable identifier of the entity that failed (same format as BulkActionRequest.Ids).
    /// </summary>
    string EntityId,
    /// <summary>
    /// Localization key or plain message explaining the failure. Typically a key like
    /// <c>"Granit:Validation:InsufficientBalance"</c> for localized rendering or a
    /// plain message when localization is not available.
    /// </summary>
    string Error);
