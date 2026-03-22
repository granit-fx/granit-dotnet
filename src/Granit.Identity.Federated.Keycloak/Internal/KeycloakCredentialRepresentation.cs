using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// DTO for deserializing Keycloak Admin API credential representations.
/// Maps to the Keycloak <c>CredentialRepresentation</c> schema returned by
/// <c>GET /admin/realms/{realm}/users/{id}/credentials</c>.
/// </summary>
internal sealed record KeycloakCredentialRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("createdDate")] long? CreatedDate);
