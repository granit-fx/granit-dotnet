using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// DTO for deserializing Keycloak Admin API client representations.
/// Maps to the Keycloak <c>ClientRepresentation</c> schema.
/// </summary>
/// <remarks>
/// Only the fields needed are modeled (<c>id</c> = Keycloak's internal UUID, <c>clientId</c> = the OIDC
/// client_id string, <c>attributes</c> = the client's custom attribute bag). Keycloak emits many more
/// properties that <see cref="System.Text.Json"/> silently ignores. <c>attributes</c> on a
/// <b>client</b> is a <c>Map&lt;String, String&gt;</c> (single string per key — unlike a <b>user</b>'s
/// multi-valued <c>Map&lt;String, List&lt;String&gt;&gt;</c>), and is populated on both the list and the
/// single-client fetch.
/// </remarks>
internal sealed record KeycloakClientRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("attributes")] Dictionary<string, string>? Attributes = null);
