namespace Granit.Taxonomy.Endpoints.Categories.Dtos;

/// <summary>Wire-shape request for <c>POST /api/v1/taxonomy/categories/{id}/assign</c>.</summary>
public sealed record AssignCategoryRequest(string TargetType, Guid TargetId);
