using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Internal DTO for Microsoft Graph API App Role definition.
/// </summary>
internal sealed record GraphAppRoleRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("isEnabled")] bool IsEnabled = true);
