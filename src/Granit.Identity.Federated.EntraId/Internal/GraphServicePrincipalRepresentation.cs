using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Internal DTO for Microsoft Graph API Service Principal representation (subset for appRoles).
/// </summary>
internal sealed record GraphServicePrincipalRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("appRoles")] List<GraphAppRoleRepresentation>? AppRoles = null);
