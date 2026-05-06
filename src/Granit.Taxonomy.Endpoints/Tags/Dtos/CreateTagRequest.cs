namespace Granit.Taxonomy.Endpoints.Tags.Dtos;

/// <summary>
/// Wire-shape request for <c>POST /api/v1/taxonomy/tags</c>.
/// </summary>
/// <param name="Scope">Domain scope (e.g. <c>"documents"</c>, <c>"global"</c>).</param>
/// <param name="Name">User-facing label (max 50 chars).</param>
/// <param name="Color">Hex colour (<c>#RRGGBB</c>).</param>
/// <param name="HideOnEntityCard">When <c>true</c>, hides the tag from entity surfaces. Optional, default <c>false</c>.</param>
public sealed record CreateTagRequest(
    string Scope,
    string Name,
    string Color,
    bool? HideOnEntityCard);
