using Granit.Validation.ServerValidation;
using Granit.Validation.UnitedKingdom.Internal;

namespace Granit.Validation.UnitedKingdom.ServerValidation;

/// <summary>
/// Registers server validators from the <c>Granit.Validation.UnitedKingdom</c> package.
/// </summary>
internal sealed class UnitedKingdomServerValidatorContributor : IServerValidatorContributor
{
    /// <inheritdoc />
    public IEnumerable<IServerValidator> GetValidators()
    {
        // --- Personal identifiers ---
        yield return new DelegatingServerValidator("Validation:InvalidUkNationalInsuranceNumber", NationalInsuranceNumberAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidUkNhsNumber", NhsNumberAlgorithm.IsValid, isSensitive: true);

        // --- Payment ---
        yield return new DelegatingServerValidator("Validation:InvalidUkSortCode", SortCodeAlgorithm.IsValid);

        // --- Tax & Company ---
        yield return new DelegatingServerValidator("Validation:InvalidUkUtr", UtrAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidUkVat", UkVatAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidUkCompaniesHouseNumber", CompaniesHouseNumberAlgorithm.IsValid);

        // --- Address ---
        yield return new DelegatingServerValidator("Validation:InvalidUkPostcode", UkPostcodeAlgorithm.IsValid);
    }
}
