using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Internal DTO for Microsoft Graph API App Role assignment.
/// </summary>
internal sealed record GraphAppRoleAssignmentRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("appRoleId")] string AppRoleId,
    [property: JsonPropertyName("principalId")] string PrincipalId,
    [property: JsonPropertyName("resourceId")] string ResourceId,
    [property: JsonPropertyName("resourceDisplayName")] string? ResourceDisplayName = null);
