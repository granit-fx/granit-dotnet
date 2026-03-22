using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// DTO for deserializing Keycloak Admin API group representations.
/// Maps to the Keycloak <c>GroupRepresentation</c> schema.
/// </summary>
internal sealed record KeycloakGroupRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("subGroups")] List<KeycloakGroupRepresentation>? SubGroups);
