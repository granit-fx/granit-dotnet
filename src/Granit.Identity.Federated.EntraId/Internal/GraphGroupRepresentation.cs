using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Internal DTO for Microsoft Graph API group representation.
/// </summary>
internal sealed record GraphGroupRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string? Description = null);
