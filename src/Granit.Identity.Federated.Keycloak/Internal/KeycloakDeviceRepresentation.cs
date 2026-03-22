using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.Keycloak.Internal;

/// <summary>
/// DTO for deserializing Keycloak Account API device representations.
/// Maps to the Keycloak <c>DeviceRepresentation</c> schema returned by
/// <c>GET /realms/{realm}/account/sessions/devices</c>.
/// </summary>
internal sealed record KeycloakDeviceRepresentation(
    [property: JsonPropertyName("ipAddress")] string? IpAddress,
    [property: JsonPropertyName("os")] string? Os,
    [property: JsonPropertyName("osVersion")] string? OsVersion,
    [property: JsonPropertyName("browser")] string? Browser,
    [property: JsonPropertyName("device")] string? Device,
    [property: JsonPropertyName("mobile")] bool Mobile,
    [property: JsonPropertyName("current")] bool Current,
    [property: JsonPropertyName("lastAccess")] long LastAccess,
    [property: JsonPropertyName("sessions")] List<KeycloakSessionRepresentation>? Sessions);
