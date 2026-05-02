using Granit.Activities.Domain;

namespace Granit.Activities.Endpoints.Dtos;

/// <summary>Wire shape for an <see cref="Activity"/>.</summary>
public sealed record ActivityResponse(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string Type,
    Guid AssignedToUserId,
    Guid? CreatedByUserId,
    DateTimeOffset DueAt,
    string? Description,
    ActivityStatus Status,
    DateTimeOffset? CompletedAt,
    Guid? CompletedByUserId,
    DateTimeOffset CreatedAt);

/// <summary>Paginated list response for <c>GET /api/activities</c>.</summary>
public sealed record ActivityListResponse(
    IReadOnlyList<ActivityResponse> Items,
    int TotalCount,
    int Page,
    int PageSize);
