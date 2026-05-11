namespace Granit.Entities.Endpoints.Dtos.BulkActions;

/// <summary>
/// Server response for a bulk action invocation.  Includes the count of successfully
/// affected rows and a list of per-row failures (if any). Allows clients to distinguish
/// partial success from total failure and retry specific rows.
/// </summary>
/// <param name="Affected">
/// Count of rows successfully affected by the action. Rows that failed are
/// not included in this count.
/// </param>
/// <param name="Failures">
/// Per-row failure details. When empty, all requested rows succeeded
/// (<see cref="Affected"/> equals request.Ids.Count).
/// </param>
public sealed record BulkActionResponse(
    int Affected,
    IReadOnlyList<BulkActionFailureResponse> Failures);

/// <summary>
/// Details of a single row's failure in a bulk operation. Clients use this to
/// surface error feedback per entity in their UI (e.g., a toast or inline message).
/// </summary>
/// <param name="EntityId">Stable identifier of the entity that failed (same format as BulkActionRequest.Ids).</param>
/// <param name="Error">
/// Localization key or plain message explaining the failure. Typically a key like
/// <c>"Granit:Validation:InsufficientBalance"</c> for localized rendering or a
/// plain message when localization is not available.
/// </param>
public sealed record BulkActionFailureResponse(
    string EntityId,
    string Error);
