namespace Granit.Taxonomy.Endpoints.Tags.Dtos;

/// <summary>
/// Wire-shape request for <c>PATCH /api/v1/taxonomy/tags/{id}</c>. Each property is
/// optional — only the supplied facets are applied.
/// </summary>
/// <param name="Name">New label, when renaming.</param>
/// <param name="Color">New hex colour, when recolouring.</param>
/// <param name="HideOnEntityCard">New flag value, when toggling visibility on entity cards.</param>
public sealed record UpdateTagRequest(
    string? Name,
    string? Color,
    bool? HideOnEntityCard);
