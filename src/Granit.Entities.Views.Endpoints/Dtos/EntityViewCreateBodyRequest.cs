using System.Text.Json.Nodes;

namespace Granit.Entities.Views.Endpoints.Dtos;

/// <summary>Request body for <c>POST /entities/{name}/views</c>.</summary>
/// <param name="BasedOn">Name of the compiled collection this view deltas over.</param>
/// <param name="Kind">View kind inherited from <paramref name="BasedOn"/>.</param>
/// <param name="Name">User-facing label.</param>
/// <param name="Description">Optional description.</param>
/// <param name="Icon">Optional icon name.</param>
/// <param name="State">JSON delta payload.</param>
public sealed record EntityViewCreateBodyRequest(
    string BasedOn,
    string Kind,
    string Name,
    string? Description,
    string? Icon,
    JsonObject State);
