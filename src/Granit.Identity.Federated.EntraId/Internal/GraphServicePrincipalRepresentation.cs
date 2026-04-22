using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Internal DTO for Microsoft Graph API Service Principal representation (subset).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Id"/> is the Entra <b>object ID</b> (a GUID, stable within the tenant).
/// <see cref="AppId"/> is the OIDC <b>client_id</b> (also a GUID, shared across tenants for
/// multi-tenant apps) — this is what Phase 2 client-role sync matches against when resolving
/// a tracked application to its Service Principal's <see cref="AppRoles"/>.
/// </para>
/// </remarks>
internal sealed record GraphServicePrincipalRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("appRoles")] List<GraphAppRoleRepresentation>? AppRoles = null)
{
    [JsonPropertyName("appId")]
    public string? AppId { get; init; }
}
