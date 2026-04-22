using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// DTO for deserializing Keycloak Admin API client representations.
/// Maps to the Keycloak <c>ClientRepresentation</c> schema.
/// </summary>
/// <remarks>
/// Only the fields needed by the client-role sync are modeled (<c>id</c> = Keycloak's
/// internal UUID, <c>clientId</c> = the OIDC client_id string). Keycloak emits many more
/// properties that <see cref="System.Text.Json"/> silently ignores.
/// </remarks>
internal sealed record KeycloakClientRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("clientId")] string ClientId);
