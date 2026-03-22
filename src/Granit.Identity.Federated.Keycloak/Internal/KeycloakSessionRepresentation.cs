using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// DTO for deserializing Keycloak Admin API session representations.
/// Maps to the Keycloak <c>UserSessionRepresentation</c> schema.
/// </summary>
internal sealed record KeycloakSessionRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("ipAddress")] string? IpAddress,
    [property: JsonPropertyName("start")] long Start,
    [property: JsonPropertyName("lastAccess")] long LastAccess,
    [property: JsonPropertyName("rememberMe")] bool RememberMe,
    [property: JsonPropertyName("clients")] Dictionary<string, string>? Clients);
