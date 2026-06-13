using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// DTO for deserializing Keycloak Admin API client representations.
/// Maps to the Keycloak <c>ClientRepresentation</c> schema.
/// </summary>
/// <remarks>
/// Only the fields needed are modeled (<c>id</c> = Keycloak's internal UUID, <c>clientId</c> = the OIDC
/// client_id string, <c>attributes</c> = the client's custom attribute bag). Keycloak emits many more
/// properties that <see cref="System.Text.Json"/> silently ignores. <c>attributes</c> is a
/// <c>Map&lt;String, List&lt;String&gt;&gt;</c> in Keycloak (multi-valued) and is only populated on a
/// single-client fetch (<c>GET /clients/{uuid}</c>), not on the list endpoint's lightweight projection.
/// </remarks>
internal sealed record KeycloakClientRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("attributes")] Dictionary<string, List<string>>? Attributes = null);
