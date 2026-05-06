namespace Granit.Taxonomy.Endpoints.Categories.Dtos;

/// <summary>Wire-shape request for <c>POST /api/v1/taxonomy/categories/{id}/move</c>.</summary>
/// <param name="NewParentId">New parent id, or <c>null</c> to convert to a root category.</param>
public sealed record MoveCategoryRequest(Guid? NewParentId);
