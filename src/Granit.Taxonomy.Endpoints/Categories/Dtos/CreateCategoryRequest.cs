namespace Granit.Taxonomy.Endpoints.Categories.Dtos;

/// <summary>Wire-shape request for <c>POST /api/v1/taxonomy/categories</c>.</summary>
/// <param name="Scope">Domain scope (e.g. <c>"products"</c>).</param>
/// <param name="ParentId">Identifier of the parent category, or <c>null</c> for a root.</param>
/// <param name="Name">User-facing label (max 100 chars; cannot contain '/').</param>
/// <param name="IconName">Optional icon identifier (e.g. Lucide name).</param>
/// <param name="HideOnEntityCard">When <c>true</c>, hides the category from entity surfaces.</param>
public sealed record CreateCategoryRequest(
    string Scope,
    Guid? ParentId,
    string Name,
    string? IconName,
    bool? HideOnEntityCard);
