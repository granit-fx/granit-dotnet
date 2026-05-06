namespace Granit.Taxonomy.Endpoints.Tags.Dtos;

/// <summary>Wire-shape response carrying a single <c>Tag</c>.</summary>
public sealed record TagResponse(
    Guid Id,
    Guid? TenantId,
    string Scope,
    string Name,
    string Color,
    bool HideOnEntityCard,
    uint RowVersion);
