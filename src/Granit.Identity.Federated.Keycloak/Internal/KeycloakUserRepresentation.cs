using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// DTO for deserializing Keycloak Admin API user representations.
/// Maps to the Keycloak <c>UserRepresentation</c> schema.
/// </summary>
internal sealed record KeycloakUserRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string? Username,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("firstName")] string? FirstName,
    [property: JsonPropertyName("lastName")] string? LastName,
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("attributes")] Dictionary<string, List<string>>? Attributes = null);
