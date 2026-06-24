using Granit.Localization;

namespace Granit.Validation.Europe;

/// <summary>
/// Marker class for the <c>ValidationEurope</c> localization resource.
/// JSON files: <c>Localization/ValidationEurope/{culture}.json</c>, embedded in this assembly.
/// </summary>
/// <remarks>
/// Covers European regulatory identifier error codes (<c>Validation:Format:French*</c>,
/// <c>Validation:Format:Belgian*</c>, <c>Validation:Format:European*</c>).
/// Inherits from <see cref="ValidationLocalizationResource"/> for shared error code resolution.
/// </remarks>
[LocalizationResourceName("ValidationEurope")]
[InheritResource(typeof(ValidationLocalizationResource))]
public sealed class ValidationEuropeLocalizationResource;
