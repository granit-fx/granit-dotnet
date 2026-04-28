namespace Granit.Localization.Endpoints.Dtos;

/// <summary>
/// Response payload for <c>GET /api/{version}/localization</c>.
/// Contains all localization resources for the requested culture and the list of available languages.
/// </summary>
/// <param name="CultureName">The resolved culture name (e.g. "fr", "en").</param>
/// <param name="Resources">
/// All registered localization resources, keyed by resource name.
/// Each value is a flat dictionary of translation key → translated value.
/// </param>
/// <param name="Languages">Languages available in the application, for a language selector UI.</param>
public sealed record ApplicationLocalizationResponse(
    string CultureName,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Resources,
    IReadOnlyList<LanguageInfoResponse> Languages);
