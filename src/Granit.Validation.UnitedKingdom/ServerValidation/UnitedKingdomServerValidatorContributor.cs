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
        yield return new DelegatingServerValidator("Validation:Format:UkNationalInsuranceNumber", NationalInsuranceNumberAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:UkNhsNumber", NhsNumberAlgorithm.IsValid, isSensitive: true);

        // --- Payment ---
        yield return new DelegatingServerValidator("Validation:Format:UkSortCode", SortCodeAlgorithm.IsValid);

        // --- Tax & Company ---
        yield return new DelegatingServerValidator("Validation:Format:UkUtr", UtrAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:UkVat", UkVatAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:UkCompaniesHouseNumber", CompaniesHouseNumberAlgorithm.IsValid);

        // --- Address ---
        yield return new DelegatingServerValidator("Validation:Format:UkPostcode", UkPostcodeAlgorithm.IsValid);
    }
}
