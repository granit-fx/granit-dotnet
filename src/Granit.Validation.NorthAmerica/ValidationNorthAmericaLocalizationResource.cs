using Granit.Localization;

namespace Granit.Validation.NorthAmerica;

/// <summary>
/// Marker class for the <c>ValidationNorthAmerica</c> localization resource.
/// JSON files: <c>Localization/ValidationNorthAmerica/{culture}.json</c>, embedded in this assembly.
/// </summary>
/// <remarks>
/// Covers North American identifier error codes (<c>Validation:Format:Us*</c>,
/// <c>Validation:Format:Canadian*</c>).
/// Inherits from <see cref="ValidationLocalizationResource"/> for shared error code resolution.
/// </remarks>
[LocalizationResourceName("ValidationNorthAmerica")]
[InheritResource(typeof(ValidationLocalizationResource))]
public sealed class ValidationNorthAmericaLocalizationResource;
