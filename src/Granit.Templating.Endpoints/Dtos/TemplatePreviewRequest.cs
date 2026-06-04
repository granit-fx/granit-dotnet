using System.Text.Json;

namespace Granit.Templating.Endpoints.Dtos;

/// <summary>
/// Request body for the template preview endpoint.
/// </summary>
/// <param name="Culture">Optional BCP 47 culture tag to select the template variant.</param>
/// <param name="Data">Optional JSON data model to merge into the template.</param>
public sealed record TemplatePreviewRequest(
    string? Culture = null,
    JsonElement? Data = null);
