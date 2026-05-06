namespace Granit.Activities.Endpoints.Dtos;

/// <summary>Body for <c>POST /api/activities</c>.</summary>
public sealed record CreateActivityRequest(
    string EntityType,
    Guid EntityId,
    string Type,
    Guid AssignedToUserId,
    DateTimeOffset DueAt,
    string? Description = null);

/// <summary>
/// Body for <c>POST /api/activities/{id}/complete</c>. The completion
/// timestamp and the actor user id are resolved server-side
/// (<c>IClock</c> + <c>ClaimsPrincipal</c>) for audit integrity.
/// </summary>
public sealed record CompleteActivityRequest;

/// <summary>
/// Body for <c>POST /api/activities/{id}/cancel</c>. The cancellation
/// timestamp and the actor user id are resolved server-side
/// (<c>IClock</c> + <c>ClaimsPrincipal</c>) for audit integrity.
/// </summary>
public sealed record CancelActivityRequest;

/// <summary>Body for <c>PUT /api/activities/{id}/assignee</c>.</summary>
public sealed record ReassignActivityRequest(Guid NewAssigneeUserId);

/// <summary>Body for <c>PUT /api/activities/{id}/due-date</c>.</summary>
public sealed record RescheduleActivityRequest(DateTimeOffset NewDueAt);
