using Granit.Localization;

namespace Granit.AI.Prompts;

/// <summary>
/// Marker class for the <c>AIPrompts</c> localization resource.
/// JSON files: <c>Localization/AIPrompts/{culture}.json</c>, embedded in this assembly.
/// Seeded system prompts store these keys in their name/description; the catalogue endpoint
/// resolves them to the request culture.
/// </summary>
[LocalizationResourceName("AIPrompts", DefaultCulture = "en")]
public sealed class AIPromptsLocalizationResource;
