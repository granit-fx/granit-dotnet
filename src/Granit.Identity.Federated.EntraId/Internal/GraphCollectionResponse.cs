using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Wrapper for Microsoft Graph API collection responses
/// (<c>{ "value": [...], "@odata.nextLink": "..." }</c>).
/// </summary>
internal sealed record GraphCollectionResponse<T>(
    [property: JsonPropertyName("value")] List<T>? Value = null,
    [property: JsonPropertyName("@odata.nextLink")] string? NextLink = null);
