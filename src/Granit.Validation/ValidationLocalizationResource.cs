using Granit.Localization;

namespace Granit.Validation;
/// <summary>
/// Marker class for the <c>Validation</c> localization resource.
/// JSON files: <c>Localization/Validation/{culture}.json</c>, embedded in this assembly.
/// </summary>
/// <remarks>
/// Covers all <c>Validation:*</c> error codes, including both custom identifier
/// validators and the built-in FluentValidation validators remapped by
/// <c>GranitErrorCodeLanguageManager</c>.
/// </remarks>
[LocalizationResourceName("Validation")]
[InheritResource(typeof(GranitLocalizationResource))]
public sealed class ValidationLocalizationResource;
