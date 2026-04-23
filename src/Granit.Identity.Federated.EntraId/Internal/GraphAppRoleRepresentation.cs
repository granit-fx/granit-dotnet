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
    [property: JsonPropertyName("isEnabled")] bool IsEnabled = true)
{
    /// <summary>
    /// Microsoft Graph requires <c>allowedMemberTypes</c> on every App Role. Defaults to
    /// <c>["User"]</c> — the only value Granit uses today; Graph also accepts
    /// <c>"Application"</c> for service-to-service roles but Granit does not manage those.
    /// </summary>
    [JsonPropertyName("allowedMemberTypes")]
    public IReadOnlyList<string> AllowedMemberTypes { get; init; } = ["User"];
}
