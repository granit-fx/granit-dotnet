namespace Granit.Activities.Endpoints.Dtos;

/// <summary>Body for <c>POST /api/activities</c>.</summary>
public sealed record CreateActivityRequest(
    string EntityType,
    Guid EntityId,
    string Type,
    Guid AssignedToUserId,
    DateTimeOffset DueAt,
    string? Description = null);

/// <summary>Body for <c>POST /api/activities/{id}/complete</c>. Empty — completion timestamp is derived server-side from <see cref="Granit.Timing.IClock"/> to prevent client-controlled backdating (VULN-101).</summary>
public sealed record CompleteActivityRequest();

/// <summary>Body for <c>POST /api/activities/{id}/cancel</c>. Empty — cancellation timestamp is derived server-side from <see cref="Granit.Timing.IClock"/>.</summary>
public sealed record CancelActivityRequest();

/// <summary>Body for <c>PUT /api/activities/{id}/assignee</c>.</summary>
public sealed record ReassignActivityRequest(Guid NewAssigneeUserId);

/// <summary>Body for <c>PUT /api/activities/{id}/due-date</c>.</summary>
public sealed record RescheduleActivityRequest(DateTimeOffset NewDueAt);
