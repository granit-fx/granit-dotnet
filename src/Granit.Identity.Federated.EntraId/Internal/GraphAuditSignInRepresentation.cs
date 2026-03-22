using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Internal DTO for Microsoft Graph API audit sign-in log entry.
/// </summary>
internal sealed record GraphAuditSignInRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("ipAddress")] string? IpAddress = null,
    [property: JsonPropertyName("createdDateTime")] DateTimeOffset? CreatedDateTime = null,
    [property: JsonPropertyName("clientAppUsed")] string? ClientAppUsed = null,
    [property: JsonPropertyName("deviceDetail")] GraphDeviceDetail? DeviceDetail = null,
    [property: JsonPropertyName("status")] GraphSignInStatus? Status = null);

/// <summary>
/// Device details from a sign-in log entry.
/// </summary>
internal sealed record GraphDeviceDetail(
    [property: JsonPropertyName("browser")] string? Browser = null,
    [property: JsonPropertyName("operatingSystem")] string? OperatingSystem = null,
    [property: JsonPropertyName("deviceId")] string? DeviceId = null,
    [property: JsonPropertyName("displayName")] string? DisplayName = null);

/// <summary>
/// Sign-in status from a sign-in log entry.
/// </summary>
internal sealed record GraphSignInStatus(
    [property: JsonPropertyName("errorCode")] int ErrorCode = 0);
