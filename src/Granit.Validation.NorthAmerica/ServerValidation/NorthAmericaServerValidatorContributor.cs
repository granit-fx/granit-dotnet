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
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUsSsn", SsnAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUsEin", EinAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUsStateCode", UsStateCodeAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUsZipCode", ZipCodeAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidNanpPhoneNumber", NanpPhoneAlgorithm.IsValid);

        // --- Canada ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidCanadianSin", SinAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidCanadianBusinessNumber", BusinessNumberAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidCanadianPostalCode", CanadianPostalCodeAlgorithm.IsValid);
    }
}
