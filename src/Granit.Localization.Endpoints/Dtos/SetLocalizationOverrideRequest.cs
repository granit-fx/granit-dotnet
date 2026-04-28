namespace Granit.Localization.Endpoints.Dtos;

/// <summary>
/// Request body for the <c>PUT /api/{version}/localization/overrides/{resourceName}/{cultureName}/{key}</c> endpoint.
/// </summary>
/// <param name="Value">Override value for the translation key. Must not be empty.</param>
public sealed record SetLocalizationOverrideRequest(string Value);
