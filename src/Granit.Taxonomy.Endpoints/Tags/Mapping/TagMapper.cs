using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Tags.Dtos;

namespace Granit.Taxonomy.Endpoints.Tags.Mapping;

internal static class TagMapper
{
    public static TagResponse ToResponse(this Tag tag) =>
        new(tag.Id, tag.TenantId, tag.Scope, tag.Name, tag.Color, tag.HideOnEntityCard, tag.RowVersion);

    public static TagAssignmentResponse ToResponse(this TagAssignment assignment) =>
        new(
            assignment.Id,
            assignment.TenantId,
            assignment.TagId,
            assignment.TargetType,
            assignment.TargetId,
            assignment.AssignedAt,
            assignment.AssignedByUserId);
}
