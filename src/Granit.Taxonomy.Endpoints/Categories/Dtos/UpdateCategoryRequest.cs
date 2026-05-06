namespace Granit.Taxonomy.Endpoints.Categories.Dtos;

/// <summary>Partial-update request for <c>PATCH /api/v1/taxonomy/categories/{id}</c>.</summary>
public sealed record UpdateCategoryRequest(
    string? Name,
    string? IconName,
    bool? HideOnEntityCard);
