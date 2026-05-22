using Granit.Validation.Europe.Extensions;
using Granit.Validation.Europe.Internal;
using Granit.Validation.Europe.Internal.Germany;
using Granit.Validation.Europe.Internal.Italy;
using Granit.Validation.Europe.Internal.Luxembourg;
using Granit.Validation.Europe.Internal.Netherlands;
using Granit.Validation.Europe.Internal.Spain;
using Granit.Validation.ServerValidation;

namespace Granit.Validation.Europe.ServerValidation;

/// <summary>
/// Registers server validators from the <c>Granit.Validation.Europe</c> package.
/// </summary>
internal sealed class EuropeServerValidatorContributor : IServerValidatorContributor
{
    /// <inheritdoc />
    public IEnumerable<IServerValidator> GetValidators()
    {
        // --- Tax identifiers ---
        yield return new DelegatingServerValidator("Validation:InvalidBelgianVat", EuropeanVatAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidFrenchVat", FrenchVatAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidEuropeanVat", EuropeanVatAlgorithm.IsValid);

        // --- Payment identifiers ---
        yield return new DelegatingServerValidator("Validation:InvalidFrenchRib", FrenchRibAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidBelgianAccountNumber", BelgianAccountNumberAlgorithm.IsValid);

        // --- Company identifiers ---
        yield return new DelegatingServerValidator("Validation:InvalidFrenchSiren", SirenAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidFrenchSiret", SiretAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidBelgianBce", BceAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidFrenchNafCode", CompanyIdentifierValidatorExtensions.IsValidFrenchNafCode);

        // --- Personal identifiers (PII — hidden from unauthenticated discovery) ---
        yield return new DelegatingServerValidator("Validation:InvalidBelgianNiss", NissAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidFrenchNir", NirAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidBelgianEid", EidAlgorithm.IsValid, isSensitive: true);

        // --- Professional registry ---
        yield return new DelegatingServerValidator("Validation:InvalidFrenchRpps", RppsAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidFrenchAdeli", AdeliAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidFrenchFiness", FinesAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidBelgianInami", InamiAlgorithm.IsValid);

        // --- Pan-European ---
        yield return new DelegatingServerValidator("Validation:InvalidEori", EoriAlgorithm.IsValid);

        // --- Address (regex-based) ---
        yield return new DelegatingServerValidator("Validation:InvalidFrenchPostalCode", AddressValidatorExtensions.IsValidFrenchPostalCode);
        yield return new DelegatingServerValidator("Validation:InvalidBelgianPostalCode", AddressValidatorExtensions.IsValidBelgianPostalCode);
        yield return new DelegatingServerValidator("Validation:InvalidFrenchInseeCode", AddressValidatorExtensions.IsValidFrenchInseeCode);

        // --- Germany ---
        yield return new DelegatingServerValidator("Validation:InvalidGermanSteuerId", SteuerIdAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidGermanPostalCode", GermanPostalCodeAlgorithm.IsValid);

        // --- Netherlands ---
        yield return new DelegatingServerValidator("Validation:InvalidDutchBsn", BsnAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidDutchKvk", KvkAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidDutchPostcode", DutchPostcodeAlgorithm.IsValid);

        // --- Italy ---
        yield return new DelegatingServerValidator("Validation:InvalidItalianCodiceFiscale", CodiceFiscaleAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidItalianPartitaIva", PartitaIvaAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidItalianPostalCode", ItalianPostalCodeAlgorithm.IsValid);

        // --- Spain ---
        yield return new DelegatingServerValidator("Validation:InvalidSpanishNif", NifAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidSpanishNie", NieAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidSpanishCif", CifAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:InvalidSpanishPostalCode", SpanishPostalCodeAlgorithm.IsValid);

        // --- Luxembourg ---
        yield return new DelegatingServerValidator("Validation:InvalidLuxembourgMatricule", LuxembourgMatriculeAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:InvalidLuxembourgRcs", LuxembourgRcsAlgorithm.IsValid);
    }
}
