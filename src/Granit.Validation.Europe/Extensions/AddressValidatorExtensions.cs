using System.Text.RegularExpressions;
using FluentValidation;
using Granit.Validation.Extensions;

namespace Granit.Validation.Europe.Extensions;

/// <summary>
/// FluentValidation extension methods for postal address fields.
/// </summary>
public static partial class AddressValidatorExtensions
{
    // French postal codes: 01000–99999 (excludes 00xxx which does not exist).
    // DOM-TOM codes 97xxx and 98xxx are included.
    [GeneratedRegex(@"^(0[1-9]|[1-9]\d)\d{3}$", RegexOptions.None, 100)]
    private static partial Regex FrenchPostalCodeRegex();

    // Belgian postal codes: 1000–9999.
    [GeneratedRegex(@"^[1-9]\d{3}$", RegexOptions.None, 100)]
    private static partial Regex BelgianPostalCodeRegex();

    // French INSEE commune code: 5 characters.
    //   Metropolitan: dept 01–95 (not 96–99 which don't exist) or 2A/2B (Corse) + 3-digit commune.
    //   DOM (971–976): 3-digit dept + 2-digit commune.
    [GeneratedRegex(@"^((0[1-9]|[1-8]\d|9[0-5]|2[AB])\d{3}|97[1-6]\d{2})$", RegexOptions.IgnoreCase, 100)]
    private static partial Regex FrenchInseeCodeRegex();

    /// <summary>
    /// Validates a French postal code (code postal).
    /// </summary>
    /// <remarks>
    /// Accepts 5-digit codes in the range 01000–99999.
    /// Includes metropolitan France, Corse (20xxx), and DOM-TOM (97xxx, 98xxx).
    /// Does not accept codes starting with 00.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchPostalCode<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && FrenchPostalCodeRegex().IsMatch(value))
            .WithErrorCodeAndMessage("Validation:Format:FrenchPostalCode");

    /// <summary>
    /// Validates a Belgian postal code (code postal / postcode).
    /// </summary>
    /// <remarks>
    /// Accepts 4-digit codes in the range 1000–9999.
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> BelgianPostalCode<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && BelgianPostalCodeRegex().IsMatch(value))
            .WithErrorCodeAndMessage("Validation:Format:BelgianPostalCode");

    /// <summary>
    /// Validates a French INSEE commune code (code officiel géographique).
    /// </summary>
    /// <remarks>
    /// Exactly 5 characters: 2-character department code + 3-digit commune for metropolitan
    /// France and Corse (depts 01–99, 2A, 2B), or 3-digit DOM department (971–976) +
    /// 2-digit commune. Case-insensitive for Corse codes (<c>2a004</c> is accepted).
    /// </remarks>
    public static IRuleBuilderOptions<T, string?> FrenchInseeCode<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(value => value != null && FrenchInseeCodeRegex().IsMatch(value))
            .WithErrorCodeAndMessage("Validation:Format:FrenchInseeCode");

    // -------------------------------------------------------------------------
    // Server-side single-field validation delegates
    // -------------------------------------------------------------------------

    internal static bool IsValidFrenchPostalCode(string? value) =>
        value is not null && FrenchPostalCodeRegex().IsMatch(value);

    internal static bool IsValidBelgianPostalCode(string? value) =>
        value is not null && BelgianPostalCodeRegex().IsMatch(value);

    internal static bool IsValidFrenchInseeCode(string? value) =>
        value is not null && FrenchInseeCodeRegex().IsMatch(value);
}
