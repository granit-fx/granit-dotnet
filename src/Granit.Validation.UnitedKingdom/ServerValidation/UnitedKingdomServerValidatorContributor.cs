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
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUkNationalInsuranceNumber", NationalInsuranceNumberAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUkNhsNumber", NhsNumberAlgorithm.IsValid);

        // --- Payment ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUkSortCode", SortCodeAlgorithm.IsValid);

        // --- Tax & Company ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUkUtr", UtrAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUkVat", UkVatAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUkCompaniesHouseNumber", CompaniesHouseNumberAlgorithm.IsValid);

        // --- Address ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUkPostcode", UkPostcodeAlgorithm.IsValid);
    }
}
