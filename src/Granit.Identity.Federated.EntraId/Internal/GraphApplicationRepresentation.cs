using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Internal DTO for Microsoft Graph <c>Application</c> resource. Used by the Phase 3
/// <c>CreateClientRoleAsync</c> path: App Roles are authored on the Application, not on
/// the Service Principal, so the write flow fetches the app + its current appRoles,
/// appends, and PATCHes the whole array back.
/// </summary>
internal sealed record GraphApplicationRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("appId")] string AppId)
{
    [JsonPropertyName("appRoles")]
    public List<GraphAppRoleRepresentation>? AppRoles { get; init; }
}
