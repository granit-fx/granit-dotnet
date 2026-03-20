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
        yield return new DelegatingServerValidator("Granit:Validation:InvalidIban", IbanAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidBicSwift", BicSwiftAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidSepaCreditorIdentifier", SepaCreditorIdentifierAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidCreditCard", CreditCardAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidLei", LeiAlgorithm.IsValid);

        // ISO standard codes (algorithm + regex)
        yield return new DelegatingServerValidator("Granit:Validation:InvalidIso4217CurrencyCode", Iso4217CurrencyCodeAlgorithm.IsValid);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidIso8601Duration", StandardValidatorExtensions.IsValidIso8601Duration);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUuid", StandardValidatorExtensions.IsValidUuid);

        // Contact identifiers (regex-based)
        yield return new DelegatingServerValidator("Granit:Validation:InvalidEmail", ContactValidatorExtensions.IsValidEmail);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidE164Phone", ContactValidatorExtensions.IsValidE164Phone);

        // Format validators (regex + algorithm)
        yield return new DelegatingServerValidator("Granit:Validation:InvalidSlug", FormatValidatorExtensions.IsValidSlug);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidBase64String", FormatValidatorExtensions.IsValidBase64String);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidColorHex", FormatValidatorExtensions.IsValidColorHex);

        // Network identifiers (regex + algorithm)
        yield return new DelegatingServerValidator("Granit:Validation:InvalidUrl", NetworkValidatorExtensions.IsValidUrl);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidIpv4Address", NetworkValidatorExtensions.IsValidIpv4Address);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidIpv6Address", NetworkValidatorExtensions.IsValidIpv6Address);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidMacAddress", NetworkValidatorExtensions.IsValidMacAddress);

        // Locale identifiers (regex-based)
        yield return new DelegatingServerValidator("Granit:Validation:InvalidIso3166Alpha2", LocaleValidatorExtensions.IsValidIso3166Alpha2);
        yield return new DelegatingServerValidator("Granit:Validation:InvalidBcp47LanguageTag", LocaleValidatorExtensions.IsValidBcp47LanguageTag);
    }
}
