using System.Text.RegularExpressions;
using FluentValidation;

namespace Granit.Validation.Extensions;

/// <summary>
/// FluentValidation extension methods for locale and internationalisation identifiers.
/// </summary>
public static partial class LocaleValidatorExtensions
{
    // ISO 3166-1 alpha-2: exactly 2 uppercase letters after normalisation.
    [GeneratedRegex(@"^[A-Z]{2}$", RegexOptions.None, 100)]
    private static partial Regex Iso3166Alpha2Regex();

    // BCP 47 language tag (practical subset):
    //   language (2–3 chars) + optional script (4 chars) + optional region (2 chars).
    //   Examples: fr, en, nl, fr-FR, fr-BE, en-US, zh-Hans, zh-Hans-CN.
    //   Numeric region subtags (e.g. 419) and extension subtags are not supported.
    [GeneratedRegex(@"^[a-z]{2,3}(-[a-z]{4})?(-[a-z]{2})?$", RegexOptions.IgnoreCase, 100)]
    private static partial Regex Bcp47Regex();

    /// <summary>
    /// Validates an ISO 3166-1 alpha-2 country code (e.g. <c>BE</c>, <c>FR</c>).
    /// </summary>
    /// <remarks>
    /// Normalised to uppercase before validation.
    /// Only the format is checked; no lookup against the official country list is performed.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Iso3166Alpha2CountryCode<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null
                && Iso3166Alpha2Regex().IsMatch(value.Trim().ToUpperInvariant()))
            .WithErrorCodeAndMessage("Validation:InvalidIso3166Alpha2");

    /// <summary>
    /// Validates a BCP 47 language tag (e.g. <c>fr</c>, <c>fr-BE</c>, <c>zh-Hans-CN</c>).
    /// </summary>
    /// <remarks>
    /// Supports a practical subset of BCP 47: 2- or 3-character primary language subtag,
    /// optional 4-character script subtag, optional 2-character region subtag.
    /// Case-insensitive. Numeric region subtags (e.g. <c>419</c>) are not supported.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> Bcp47LanguageTag<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && Bcp47Regex().IsMatch(value.Trim()))
            .WithErrorCodeAndMessage("Validation:InvalidBcp47LanguageTag");

    // -------------------------------------------------------------------------
    // Server-side single-field validation delegates
    // -------------------------------------------------------------------------

    internal static bool IsValidIso3166Alpha2(string? value) =>
        value is not null && Iso3166Alpha2Regex().IsMatch(value.Trim().ToUpperInvariant());

    internal static bool IsValidBcp47LanguageTag(string? value) =>
        value is not null && Bcp47Regex().IsMatch(value.Trim());
}
