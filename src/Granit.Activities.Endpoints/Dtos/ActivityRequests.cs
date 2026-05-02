namespace Granit.Activities.Endpoints.Dtos;

/// <summary>Body for <c>POST /api/activities</c>.</summary>
public sealed record CreateActivityRequest(
    string EntityType,
    Guid EntityId,
    string Type,
    Guid AssignedToUserId,
    DateTimeOffset DueAt,
    string? Description = null);

/// <summary>Body for <c>POST /api/activities/{id}/complete</c>.</summary>
public sealed record CompleteActivityRequest(DateTimeOffset CompletedAt);

/// <summary>Body for <c>POST /api/activities/{id}/cancel</c>.</summary>
public sealed record CancelActivityRequest(DateTimeOffset CancelledAt);

/// <summary>Body for <c>PUT /api/activities/{id}/assignee</c>.</summary>
public sealed record ReassignActivityRequest(Guid NewAssigneeUserId);

/// <summary>Body for <c>PUT /api/activities/{id}/due-date</c>.</summary>
public sealed record RescheduleActivityRequest(DateTimeOffset NewDueAt);
