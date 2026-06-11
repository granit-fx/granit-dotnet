using Granit.Validation.Extensions;
using Granit.Validation.Internal;

namespace Granit.Validation.ServerValidation;

/// <summary>
/// Registers server validators from the <c>Granit.Validation</c> base package.
/// </summary>
/// <remarks>
/// Excludes <c>GeoLatitude</c>/<c>GeoLongitude</c> — those operate on <c>double</c>/<c>decimal</c>,
/// not <c>string?</c>.
/// </remarks>
internal sealed class CoreServerValidatorContributor : IServerValidatorContributor
{
    /// <inheritdoc />
    public IEnumerable<IServerValidator> GetValidators()
    {
        // Payment identifiers (algorithm-based)
        yield return new DelegatingServerValidator("Validation:Format:Iban", IbanAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:BicSwift", BicSwiftAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:SepaCreditorIdentifier", SepaCreditorIdentifierAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:CreditCard", CreditCardAlgorithm.IsValid, isSensitive: true);
        yield return new DelegatingServerValidator("Validation:Format:Lei", LeiAlgorithm.IsValid);

        // ISO standard codes (algorithm + regex)
        yield return new DelegatingServerValidator("Validation:Format:Iso4217CurrencyCode", Iso4217CurrencyCodeAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Validation:Format:Iso8601Duration", StandardValidatorExtensions.IsValidIso8601Duration);
        yield return new DelegatingServerValidator("Validation:Format:Uuid", StandardValidatorExtensions.IsValidUuid);

        // Party identifiers (regex-based)
        yield return new DelegatingServerValidator("Validation:Format:Email", ContactValidatorExtensions.IsValidEmail);
        yield return new DelegatingServerValidator("Validation:Format:E164Phone", ContactValidatorExtensions.IsValidE164Phone);

        // Format validators (regex + algorithm)
        yield return new DelegatingServerValidator("Validation:Format:Slug", FormatValidatorExtensions.IsValidSlug);
        yield return new DelegatingServerValidator("Validation:Format:Base64String", FormatValidatorExtensions.IsValidBase64String);
        yield return new DelegatingServerValidator("Validation:Format:ColorHex", FormatValidatorExtensions.IsValidColorHex);

        // Network identifiers (regex + algorithm)
        yield return new DelegatingServerValidator("Validation:Format:Url", NetworkValidatorExtensions.IsValidUrl);
        yield return new DelegatingServerValidator("Validation:Format:Ipv4Address", NetworkValidatorExtensions.IsValidIpv4Address);
        yield return new DelegatingServerValidator("Validation:Format:Ipv6Address", NetworkValidatorExtensions.IsValidIpv6Address);
        yield return new DelegatingServerValidator("Validation:Format:MacAddress", NetworkValidatorExtensions.IsValidMacAddress);

        // Locale identifiers (regex-based)
        yield return new DelegatingServerValidator("Validation:Format:Iso3166Alpha2", LocaleValidatorExtensions.IsValidIso3166Alpha2);
        yield return new DelegatingServerValidator("Validation:Format:Bcp47LanguageTag", LocaleValidatorExtensions.IsValidBcp47LanguageTag);
    }
}
