using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// DTO for deserializing Keycloak Admin API role representations.
/// Maps to the Keycloak <c>RoleRepresentation</c> schema.
/// </summary>
internal sealed record KeycloakRoleRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description);
