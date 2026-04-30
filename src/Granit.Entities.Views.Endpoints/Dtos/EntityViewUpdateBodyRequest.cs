using System.Text.Json.Nodes;

namespace Granit.Entities.Views.Endpoints.Dtos;

/// <summary>Request body for <c>PUT /entities/{name}/views/{id}</c>.</summary>
/// <param name="Name">User-facing label.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Icon">Optional icon name.</param>
/// <param name="State">JSON delta payload — validated against the view's existing <c>Kind</c>.</param>
public sealed record EntityViewUpdateBodyRequest(
    string Name,
    string? Description,
    string? Icon,
    JsonObject State);
