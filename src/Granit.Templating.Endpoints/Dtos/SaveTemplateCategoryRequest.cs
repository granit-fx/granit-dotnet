namespace Granit.Templating.Endpoints.Dtos;

/// <summary>
/// Request body for creating or updating a template category.
/// </summary>
/// <param name="Name">Display name (required, max 200 characters).</param>
/// <param name="Description">Optional description (max 500 characters).</param>
/// <param name="Icon">Optional Lucide icon name (max 100 characters).</param>
/// <param name="SortOrder">Display order (default 0).</param>
public sealed record SaveTemplateCategoryRequest(
    string Name,
    string? Description = null,
    string? Icon = null,
    int SortOrder = 0);
