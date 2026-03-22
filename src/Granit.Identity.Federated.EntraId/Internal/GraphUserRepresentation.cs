using System.Text.Json.Serialization;

namespace Granit.Identity.Federated.EntraId.Internal;

/// <summary>
/// Internal DTO for Microsoft Graph API user representation.
/// </summary>
internal sealed record GraphUserRepresentation(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("userPrincipalName")] string? UserPrincipalName,
    [property: JsonPropertyName("mail")] string? Mail,
    [property: JsonPropertyName("givenName")] string? GivenName,
    [property: JsonPropertyName("surname")] string? Surname,
    [property: JsonPropertyName("accountEnabled")] bool AccountEnabled,
    [property: JsonPropertyName("displayName")] string? DisplayName = null,
    [property: JsonPropertyName("lastPasswordChangeDateTime")] DateTimeOffset? LastPasswordChangeDateTime = null,
    [property: JsonPropertyName("onPremisesExtensionAttributes")] GraphExtensionAttributes? ExtensionAttributes = null);

/// <summary>
/// Azure AD extension attributes (extensionAttribute1–15).
/// </summary>
internal sealed record GraphExtensionAttributes(
    [property: JsonPropertyName("extensionAttribute1")] string? ExtensionAttribute1 = null,
    [property: JsonPropertyName("extensionAttribute2")] string? ExtensionAttribute2 = null,
    [property: JsonPropertyName("extensionAttribute3")] string? ExtensionAttribute3 = null,
    [property: JsonPropertyName("extensionAttribute4")] string? ExtensionAttribute4 = null,
    [property: JsonPropertyName("extensionAttribute5")] string? ExtensionAttribute5 = null,
    [property: JsonPropertyName("extensionAttribute6")] string? ExtensionAttribute6 = null,
    [property: JsonPropertyName("extensionAttribute7")] string? ExtensionAttribute7 = null,
    [property: JsonPropertyName("extensionAttribute8")] string? ExtensionAttribute8 = null,
    [property: JsonPropertyName("extensionAttribute9")] string? ExtensionAttribute9 = null,
    [property: JsonPropertyName("extensionAttribute10")] string? ExtensionAttribute10 = null,
    [property: JsonPropertyName("extensionAttribute11")] string? ExtensionAttribute11 = null,
    [property: JsonPropertyName("extensionAttribute12")] string? ExtensionAttribute12 = null,
    [property: JsonPropertyName("extensionAttribute13")] string? ExtensionAttribute13 = null,
    [property: JsonPropertyName("extensionAttribute14")] string? ExtensionAttribute14 = null,
    [property: JsonPropertyName("extensionAttribute15")] string? ExtensionAttribute15 = null);
