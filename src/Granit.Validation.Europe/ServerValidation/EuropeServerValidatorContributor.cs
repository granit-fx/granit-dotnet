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
        yield return new DelegatingServerValidator("Validation:Format:BelgianVat", EuropeanVatAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:FrenchVat", FrenchVatAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:EuropeanVat", EuropeanVatAlgorithm.IsValid);

        // --- Payment identifiers ---
        yield return new DelegatingServerValidator("Validation:Format:FrenchRib", FrenchRibAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:BelgianAccountNumber", BelgianAccountNumberAlgorithm.IsValid);

        // --- Company identifiers ---
        yield return new DelegatingServerValidator("Validation:Format:FrenchSiren", SirenAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:FrenchSiret", SiretAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:BelgianBce", BceAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:FrenchNafCode", CompanyIdentifierValidatorExtensions.IsValidFrenchNafCode);

        // --- Personal identifiers (PII — hidden from unauthenticated discovery) ---
        yield return new DelegatingServerValidator("Validation:Format:BelgianNiss", NissAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:FrenchNir", NirAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:BelgianEid", EidAlgorithm.IsValid, isSensitive: true);

        // --- Professional registry ---
        yield return new DelegatingServerValidator("Validation:Format:FrenchRpps", RppsAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:FrenchAdeli", AdeliAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:FrenchFiness", FinesAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:BelgianInami", InamiAlgorithm.IsValid);

        // --- Pan-European ---
        yield return new DelegatingServerValidator("Validation:Format:Eori", EoriAlgorithm.IsValid);

        // --- Address (regex-based) ---
        yield return new DelegatingServerValidator("Validation:Format:FrenchPostalCode", AddressValidatorExtensions.IsValidFrenchPostalCode);
        yield return new DelegatingServerValidator("Validation:Format:BelgianPostalCode", AddressValidatorExtensions.IsValidBelgianPostalCode);
        yield return new DelegatingServerValidator("Validation:Format:FrenchInseeCode", AddressValidatorExtensions.IsValidFrenchInseeCode);

        // --- Germany ---
        yield return new DelegatingServerValidator("Validation:Format:GermanSteuerId", SteuerIdAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:GermanPostalCode", GermanPostalCodeAlgorithm.IsValid);

        // --- Netherlands ---
        yield return new DelegatingServerValidator("Validation:Format:DutchBsn", BsnAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:DutchKvk", KvkAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:DutchPostcode", DutchPostcodeAlgorithm.IsValid);

        // --- Italy ---
        yield return new DelegatingServerValidator("Validation:Format:ItalianCodiceFiscale", CodiceFiscaleAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:ItalianPartitaIva", PartitaIvaAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:ItalianPostalCode", ItalianPostalCodeAlgorithm.IsValid);

        // --- Spain ---
        yield return new DelegatingServerValidator("Validation:Format:SpanishNif", NifAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:SpanishNie", NieAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:SpanishCif", CifAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:SpanishPostalCode", SpanishPostalCodeAlgorithm.IsValid);

        // --- Luxembourg ---
        yield return new DelegatingServerValidator("Validation:Format:LuxembourgMatricule", LuxembourgMatriculeAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:LuxembourgRcs", LuxembourgRcsAlgorithm.IsValid);
    }
}
