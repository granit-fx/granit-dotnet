namespace Granit.Taxonomy.Endpoints.Categories.Dtos;

/// <summary>Wire-shape response carrying a single <c>Category</c>.</summary>
public sealed record CategoryResponse(
    Guid Id,
    Guid? TenantId,
    string Scope,
    Guid? ParentId,
    string Name,
    string Path,
    int Depth,
    string? IconName,
    bool HideOnEntityCard,
    uint RowVersion);

/// <summary>Wire-shape response for the per-parent listing.</summary>
public sealed record ListCategoriesResponse(IReadOnlyList<CategoryResponse> Items);

/// <summary>Wire-shape response combining a category with its breadcrumb chain.</summary>
public sealed record CategoryDetailResponse(
    CategoryResponse Category,
    IReadOnlyList<CategoryResponse> Breadcrumb);

/// <summary>Wire-shape response for a <c>CategoryAssignment</c> row.</summary>
public sealed record CategoryAssignmentResponse(
    Guid Id,
    Guid? TenantId,
    Guid CategoryId,
    string TargetType,
    Guid TargetId,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId);
