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
        yield return new DelegatingServerValidator("Granit:Validation:InvalidBelgianVat", EuropeanVatAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchVat", FrenchVatAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidEuropeanVat", EuropeanVatAlgorithm.IsValid);

        // --- Payment identifiers ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchRib", FrenchRibAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidBelgianAccountNumber", BelgianAccountNumberAlgorithm.IsValid);

        // --- Company identifiers ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchSiren", SirenAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchSiret", SiretAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidBelgianBce", BceAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchNafCode", CompanyIdentifierValidatorExtensions.IsValidFrenchNafCode);

        // --- Personal identifiers (PII — hidden from unauthenticated discovery) ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidBelgianNiss", NissAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchNir", NirAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidBelgianEid", EidAlgorithm.IsValid, isSensitive: true);

        // --- Professional registry ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchRpps", RppsAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchAdeli", AdeliAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchFiness", FinesAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidBelgianInami", InamiAlgorithm.IsValid);

        // --- Pan-European ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidEori", EoriAlgorithm.IsValid);

        // --- Address (regex-based) ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchPostalCode", AddressValidatorExtensions.IsValidFrenchPostalCode);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidBelgianPostalCode", AddressValidatorExtensions.IsValidBelgianPostalCode);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidFrenchInseeCode", AddressValidatorExtensions.IsValidFrenchInseeCode);

        // --- Germany ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidGermanSteuerId", SteuerIdAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidGermanPostalCode", GermanPostalCodeAlgorithm.IsValid);

        // --- Netherlands ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidDutchBsn", BsnAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidDutchKvk", KvkAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidDutchPostcode", DutchPostcodeAlgorithm.IsValid);

        // --- Italy ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidItalianCodiceFiscale", CodiceFiscaleAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidItalianPartitaIva", PartitaIvaAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidItalianPostalCode", ItalianPostalCodeAlgorithm.IsValid);

        // --- Spain ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidSpanishNif", NifAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidSpanishNie", NieAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidSpanishCif", CifAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidSpanishPostalCode", SpanishPostalCodeAlgorithm.IsValid);

        // --- Luxembourg ---
        yield return new DelegatingServerValidator("Granit:Validation:InvalidLuxembourgMatricule", LuxembourgMatriculeAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidLuxembourgRcs", LuxembourgRcsAlgorithm.IsValid);
    }
}
