using Granit.Activities.Domain;
using Granit.Activities.Endpoints.Dtos;

namespace Granit.Activities.Endpoints.Internal;

internal static class ActivityResponseMapper
{
    public static ActivityResponse ToResponse(this Activity activity) =>
        new(
            Id: activity.Id,
            EntityType: activity.EntityType,
            EntityId: activity.EntityId,
            Type: activity.Type,
            AssignedToUserId: activity.AssignedToUserId,
            CreatedByUserId: activity.CreatedByUserId,
            DueAt: activity.DueAt,
            Description: activity.Description,
            Status: activity.Status,
            CompletedAt: activity.CompletedAt,
            CompletedByUserId: activity.CompletedByUserId,
            CreatedAt: activity.CreatedAt);
}
