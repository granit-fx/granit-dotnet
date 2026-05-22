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
        yield return new DelegatingServerValidator("Validation:InvalidUsSsn", SsnAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidUsEin", EinAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidUsStateCode", UsStateCodeAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidUsZipCode", ZipCodeAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidNanpPhoneNumber", NanpPhoneAlgorithm.IsValid);

        // --- Canada ---
        yield return new DelegatingServerValidator("Validation:InvalidCanadianSin", SinAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidCanadianBusinessNumber", BusinessNumberAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidCanadianPostalCode", CanadianPostalCodeAlgorithm.IsValid);
    }
}
