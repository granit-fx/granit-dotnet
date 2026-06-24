using Granit.Validation.NorthAmerica.Internal.Canada;
using Granit.Validation.NorthAmerica.Internal.UnitedStates;
using Granit.Validation.ServerValidation;

namespace Granit.Validation.NorthAmerica.ServerValidation;

/// <summary>
/// Registers server validators from the <c>Granit.Validation.NorthAmerica</c> package.
/// </summary>
internal sealed class NorthAmericaServerValidatorContributor : IServerValidatorContributor
{
    /// <inheritdoc />
    public IEnumerable<IServerValidator> GetValidators()
    {
        // --- United States ---
        yield return new DelegatingServerValidator("Validation:Format:UsSsn", SsnAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:UsEin", EinAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:UsStateCode", UsStateCodeAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:UsZipCode", ZipCodeAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:NanpPhoneNumber", NanpPhoneAlgorithm.IsValid);

        // --- Canada ---
        yield return new DelegatingServerValidator("Validation:Format:CanadianSin", SinAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:CanadianBusinessNumber", BusinessNumberAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:CanadianPostalCode", CanadianPostalCodeAlgorithm.IsValid);
    }
}
