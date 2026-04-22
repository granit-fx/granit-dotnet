using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// DTO for deserializing Keycloak Admin API role representations.
/// Maps to the Keycloak <c>RoleRepresentation</c> schema.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ContainerId"/> and <see cref="ClientRole"/> are emitted by Keycloak v17+ on
/// every <c>RoleRepresentation</c>. For realm-level roles: <c>ContainerId = realm name</c>,
/// <c>ClientRole = false</c>. For client-level roles: <c>ContainerId = client UUID</c>,
/// <c>ClientRole = true</c>. Both are nullable to tolerate older Keycloak versions; if
/// absent, realm-role semantics are assumed.
/// </para>
/// </remarks>
internal sealed record KeycloakRoleRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description)
{
    /// <summary>Realm name for realm-roles; client UUID for client-roles. Nullable on Keycloak &lt; v17.</summary>
    [JsonPropertyName("containerId")]
    public string? ContainerId { get; init; }

    /// <summary><see langword="true"/> when this role is client-scoped. Nullable on Keycloak &lt; v17.</summary>
    [JsonPropertyName("clientRole")]
    public bool? ClientRole { get; init; }
}
