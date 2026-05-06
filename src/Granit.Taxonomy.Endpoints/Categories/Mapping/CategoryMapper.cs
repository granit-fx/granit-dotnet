using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Endpoints.Categories.Dtos;

namespace Granit.Taxonomy.Endpoints.Categories.Mapping;

internal static class CategoryMapper
{
    public static CategoryResponse ToResponse(this Category category) =>
        new(category.Id, category.TenantId, category.Scope, category.ParentId,
            category.Name, category.Path, category.Depth, category.IconName,
            category.HideOnEntityCard, category.RowVersion);

    public static CategoryAssignmentResponse ToResponse(this CategoryAssignment assignment) =>
        new(assignment.Id, assignment.TenantId, assignment.CategoryId,
            assignment.TargetType, assignment.TargetId,
            assignment.AssignedAt, assignment.AssignedByUserId);
}
