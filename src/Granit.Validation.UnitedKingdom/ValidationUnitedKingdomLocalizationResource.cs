using Granit.Localization;

namespace Granit.Validation.UnitedKingdom;

/// <summary>
/// Marker class for the <c>ValidationUnitedKingdom</c> localization resource.
/// JSON files: <c>Localization/ValidationUnitedKingdom/{culture}.json</c>, embedded in this assembly.
/// </summary>
/// <remarks>
/// Covers United Kingdom identifier error codes (<c>Validation:Format:Uk*</c>).
/// Inherits from <see cref="ValidationLocalizationResource"/> for shared error code resolution.
/// </remarks>
[LocalizationResourceName("ValidationUnitedKingdom")]
[InheritResource(typeof(ValidationLocalizationResource))]
public sealed class ValidationUnitedKingdomLocalizationResource;
