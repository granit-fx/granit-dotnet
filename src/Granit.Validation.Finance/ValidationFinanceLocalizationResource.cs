using Granit.Localization;

namespace Granit.Validation.Finance;

/// <summary>
/// Marker class for the <c>ValidationFinance</c> localization resource.
/// JSON files: <c>Localization/ValidationFinance/{culture}.json</c>, embedded in this assembly.
/// </summary>
/// <remarks>
/// Covers banking/payment format error codes (<c>Validation:Format:Iban</c>,
/// <c>Validation:Format:BicSwift</c>, <c>Validation:Format:SepaCreditorIdentifier</c>,
/// <c>Validation:Format:AbaRouting</c>, <c>Validation:Format:Bsb</c>,
/// <c>Validation:Format:CanadianRouting</c>, <c>Validation:Format:Ifsc</c>).
/// Inherits from <see cref="ValidationLocalizationResource"/> for shared error code resolution.
/// </remarks>
[LocalizationResourceName("ValidationFinance")]
[InheritResource(typeof(ValidationLocalizationResource))]
public sealed class ValidationFinanceLocalizationResource;
